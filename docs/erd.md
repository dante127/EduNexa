# SchoolOS — Entity Relationship Design (ERD)

> **Status:** Phase 1 deliverable (draft v1, 2026-10-12).
> **Companion docs:** `requirements.md`, `architecture.md`, `database-design.md` (full column/type/constraint details), `security-plan.md`, `implementation-plan.md`.
>
> Conventions: entity names singular PascalCase (tables snake_case in `database-design.md`); primary keys are UUIDv7 unless noted; instants are UTC (`timestamptz`), calendar dates are plain `date` in school-local terms. Diagrams are split per module (PostgreSQL schema) — cross-module references are by ID and shown as `(ext)`.

---

## 1. Identity & Access — schema `identity`

```mermaid
erDiagram
    USER ||--o{ USER_ROLE : "has (0..n)"
    ROLE ||--o{ USER_ROLE : "assigned via"
    ROLE ||--o{ ROLE_PERMISSION : "grants"
    PERMISSION ||--o{ ROLE_PERMISSION : "granted by"
    USER ||--o{ REFRESH_TOKEN : "owns sessions"

    USER {
        uuid id PK
        string email UK
        string user_name UK
        string password_hash
        string status "Invited|Active|Deactivated"
        string preferred_language "en|ar"
        timestamptz last_login_at
    }
    ROLE {
        uuid id PK
        string code UK "SuperAdmin|SchoolAdmin|Principal|Teacher|Accountant|Receptionist|Parent|Student"
        string name_en
        string name_ar
    }
    PERMISSION {
        uuid id PK
        string code UK "e.g. grades.approve"
        string module
    }
    REFRESH_TOKEN {
        uuid id PK
        uuid user_id FK
        string token_hash UK
        timestamptz expires_at
        timestamptz revoked_at
        uuid replaced_by_id FK
    }
```

- `User` is the single account record for every persona; role codes are fixed (the 8 brief roles), permissions form a catalog seeded in DB and are grouped into roles.
- `Student` and `Guardian` logins link back to their `User` row (see §3); `StaffMember` optionally links to a `User` (staff with portal accounts).
- `RefreshToken` rows are hashed; reuse detection revokes the token family.

## 2. School & Structure — schema `structure`

```mermaid
erDiagram
    SCHOOL ||--o{ ACADEMIC_YEAR : "runs"
    ACADEMIC_YEAR ||--o{ ACADEMIC_TERM : "contains"
    GRADE_LEVEL ||--o{ CLASS_SECTION : "has sections per year"
    ACADEMIC_YEAR ||--o{ CLASS_SECTION : "scopes"
    SUBJECT ||--o{ TEACHER_ASSIGNMENT : "assigned across"
    CLASS_SECTION ||--o{ TEACHER_ASSIGNMENT : "staffed by"
    CLASS_SECTION ||--o{ TIMETABLE_SLOT : "scheduled"
    SUBJECT ||--o{ TIMETABLE_SLOT : "taught in"
    CLASSROOM ||--o{ TIMETABLE_SLOT : "hosts"

    SCHOOL {
        uuid id PK
        string name_en
        string name_ar
        string timezone "IANA, e.g. Asia/Damascus"
        string default_locale "en|ar"
        string currency_code
    }
    ACADEMIC_YEAR {
        uuid id PK
        string name UK "2026-2027"
        date start_date
        date end_date
        string status "Planned|Active|Closed"
    }
    ACADEMIC_TERM {
        uuid id PK
        uuid academic_year_id FK
        string name "Semester 1"
        int sequence
        date start_date
        date end_date
        string status "Planned|Active|Closed"
    }
    GRADE_LEVEL {
        uuid id PK
        string name_en
        string name_ar
        int sequence
    }
    CLASS_SECTION {
        uuid id PK
        uuid academic_year_id FK
        uuid grade_level_id FK
        string name "A, B, ..."
        int capacity
    }
    SUBJECT {
        uuid id PK
        string code UK
        string name_en
        string name_ar
        boolean is_active
    }
    CLASSROOM {
        uuid id PK
        string name UK
        int capacity
    }
    TEACHER_ASSIGNMENT {
        uuid id PK
        uuid academic_year_id FK
        uuid staff_member_id FK "(ext staff)"
        uuid subject_id FK
        uuid class_section_id FK
    }
    TIMETABLE_SLOT {
        uuid id PK
        uuid academic_year_id FK
        uuid academic_term_id FK
        uuid class_section_id FK
        uuid subject_id FK
        uuid staff_member_id FK "(ext staff)"
        uuid classroom_id FK
        int day_of_week "0..6"
        time start_time
        time end_time
    }
```

Key rules:

- **Class sections belong to an academic year** — sections are re-created (or cloned) per year, so timetable, attendance, and enrollments never leak across years.
- `TeacherAssignment` is unique per (staff, subject, section, year).
- `TimetableSlot` conflicts (same teacher / same classroom / same section overlapping in time) are rejected — enforced with PostgreSQL exclusion constraints (see `database-design.md` §8) plus application checks.
- `School` is a singleton settings row per deployment (Q6: single school).

## 3. Students — schema `students`

```mermaid
erDiagram
    ADMISSION_APPLICATION }o--o| STUDENT : "converts to"
    STUDENT ||--o{ STUDENT_GUARDIAN : "linked via"
    GUARDIAN ||--o{ STUDENT_GUARDIAN : "linked via"
    STUDENT ||--o{ ENROLLMENT : "enrolls per year"
    STUDENT ||--o{ STUDENT_DOCUMENT : "owns"
    USER }o--o| STUDENT : "student login (ext identity)"
    USER }o--o{ GUARDIAN : "parent login (ext identity)"

    STUDENT {
        uuid id PK
        string student_number UK "YYYY-####"
        string first_name_en
        string last_name_en
        string first_name_ar
        string last_name_ar
        date date_of_birth
        string gender
        string status "Applicant|Active|Transferred|Withdrawn|Graduated"
    }
    ADMISSION_APPLICATION {
        uuid id PK
        string applicant_name
        uuid desired_grade_level_id FK "(ext structure)"
        uuid academic_year_id FK "(ext structure)"
        string status "Submitted|UnderReview|Approved|Rejected|Enrolled"
        uuid reviewed_by_user_id FK "(ext identity)"
    }
    GUARDIAN {
        uuid id PK
        string full_name_en
        string full_name_ar
        string phone
        string email
    }
    STUDENT_GUARDIAN {
        uuid id PK
        uuid student_id FK
        uuid guardian_id FK
        string relationship "Father|Mother|..."
        boolean is_primary
        boolean is_emergency_contact
        boolean is_authorized_pickup
    }
    ENROLLMENT {
        uuid id PK
        uuid student_id FK
        uuid academic_year_id FK "(ext structure)"
        uuid grade_level_id FK "(ext structure)"
        uuid class_section_id FK "(ext structure)"
        string status "Enrolled|Completed|Transferred|Withdrawn|Graduated"
        date start_date
        date end_date
    }
    STUDENT_DOCUMENT {
        uuid id PK
        uuid student_id FK
        string document_type
        string storage_path
        string verification_status
    }
```

Key rules:

- **Enrollment history is preserved**: transfers/withdrawals/graduation close the open row (`end_date`, outcome status) — never overwritten; year progression opens a new row; a same-year section change updates the open row and is fully audited (see `database-design.md` §4). Unique constraint: at most one open (`Enrolled`) row per student+year.
- `StudentGuardian` is many-to-many with flags; every portal request re-verifies an active link (parent-child enforcement, see `security-plan.md`).
- Student numbers are immutable once assigned (Q7 format).

## 4. Staff — schema `staff`

```mermaid
erDiagram
    STAFF_MEMBER {
        uuid id PK
        uuid user_id FK "(ext identity, nullable)"
        string employee_number UK
        string full_name_en
        string full_name_ar
        string position "Teacher|Accountant|..."
        string department
        date hire_date
        string contract_type
        string status "Active|OnLeave|Terminated"
    }
```

- Workload and teaching schedule are **derived** from `TeacherAssignment` and `TimetableSlot` — not duplicated here.
- Staff attendance (optional per config, Q4) reuses the attendance module with a staff-specific session type (documented in `database-design.md`).

## 5. Attendance — schema `attendance`

```mermaid
erDiagram
    ATTENDANCE_SESSION ||--o{ ATTENDANCE_RECORD : "contains one row per student"
    CLASS_SECTION ||--o{ ATTENDANCE_SESSION : "held for (ext)"

    ATTENDANCE_SESSION {
        uuid id PK
        uuid class_section_id FK "(ext structure)"
        date attendance_date
        string session_type "Daily|Period"
        int period_number "nullable"
        uuid subject_id FK "(ext structure, nullable)"
        uuid taken_by_staff_id FK "(ext staff)"
        timestamptz locked_at "nullable"
    }
    ATTENDANCE_RECORD {
        uuid id PK
        uuid attendance_session_id FK
        uuid student_id FK "(ext students)"
        string status "Present|Absent|Late|Excused"
        int minutes_late "nullable"
        string reason "nullable"
        string note "nullable"
        uuid recorded_by_staff_id FK "(ext staff)"
        timestamptz corrected_at "nullable"
        uuid corrected_by_staff_id FK "(ext staff, nullable)"
        string correction_reason "nullable"
    }
```

Key rules:

- **Duplicate prevention:** unique (attendance_session_id, student_id) at the database level.
- Corrections mutate status/fields but always record `corrected_at/by/reason`; the full before/after trail lives in `AuditLog`.
- Recurring-absence detection is a scheduled job that reads these records and emits notifications.

## 6. Academics — schema `academics`

```mermaid
erDiagram
    GRADING_POLICY ||--o{ SUBJECT : "defaults for (ext)"
    ASSESSMENT ||--o{ ASSESSMENT_COMPONENT : "split into"
    ASSESSMENT_COMPONENT ||--o{ GRADE : "scored in"
    STUDENT ||--o{ GRADE : "receives (ext)"
    GRADE ||--o{ GRADE_CHANGE : "change trail"
    STUDENT ||--o{ TERM_SUBJECT_RESULT : "gets (ext)"
    STUDENT ||--o{ REPORT_CARD : "issued (ext)"

    GRADING_POLICY {
        uuid id PK
        uuid grade_level_id FK "(ext structure, nullable = school default)"
        numeric pass_mark "e.g. 50.00"
        string rounding_mode "e.g. HalfUp"
        int display_decimals "2"
    }
    ASSESSMENT {
        uuid id PK
        uuid academic_term_id FK "(ext structure)"
        uuid class_section_id FK "(ext structure)"
        uuid subject_id FK "(ext structure)"
        uuid staff_member_id FK "(ext staff)"
        string type "Quiz|Exam|Practical|..."
        string title
        numeric max_marks
        numeric weight_percent "across subject-term, should sum to 100"
        date assessment_date
        string status "Draft|Open|Closed|Approved|Published"
    }
    ASSESSMENT_COMPONENT {
        uuid id PK
        uuid assessment_id FK
        string title
        numeric max_marks
        numeric weight_percent "within assessment"
    }
    GRADE {
        uuid id PK
        uuid assessment_component_id FK
        uuid student_id FK "(ext students)"
        numeric score
        string status "Draft|Submitted|Approved|Published"
        uuid entered_by_staff_id FK "(ext staff)"
        uuid approved_by_user_id FK "(ext identity, nullable)"
    }
    GRADE_CHANGE {
        uuid id PK
        uuid grade_id FK
        numeric old_score
        numeric new_score
        string reason
        uuid changed_by_user_id FK "(ext identity)"
        timestamptz changed_at
    }
    TERM_SUBJECT_RESULT {
        uuid id PK
        uuid student_id FK "(ext students)"
        uuid academic_term_id FK "(ext structure)"
        uuid subject_id FK "(ext structure)"
        numeric average
        string outcome "Pass|Fail"
    }
    REPORT_CARD {
        uuid id PK
        uuid student_id FK "(ext students)"
        uuid academic_term_id FK "(ext structure)"
        numeric overall_average
        timestamptz published_at
    }
```

Key rules:

- Weights: component weights within an assessment and assessment weights within a subject-term are validated to sum to 100 (±0.01) before approval; max marks are validated consistently.
- Grade lifecycle: teacher draft → submit → principal/admin approval → publication. After publication, edits only via the audited correction flow that writes `GradeChange` (old, new, reason, actor, timestamp).
- `TermSubjectResult` and `ReportCard` are **materialized snapshots created at publication** — issued documents are immutable; later corrections create revised versions, never silent rewrites.

## 7. Finance — schema `finance`

```mermaid
erDiagram
    FEE_STRUCTURE ||--o{ STUDENT_INVOICE : "billed per student (ext)"
    STUDENT_INVOICE ||--o{ INVOICE_LINE : "contains"
    STUDENT_INVOICE ||--o{ INSTALLMENT : "scheduled as"
    DISCOUNT_GRANT }o--o{ STUDENT_INVOICE : "reduces"
    PAYMENT ||--o{ PAYMENT_ALLOCATION : "allocated via"
    STUDENT_INVOICE ||--o{ PAYMENT_ALLOCATION : "settled by"
    PAYMENT ||--o{ REFUND : "refunded by"

    FEE_STRUCTURE {
        uuid id PK
        uuid academic_year_id FK "(ext structure)"
        uuid grade_level_id FK "(ext structure)"
        string fee_type "Tuition|Registration|Activities|..."
        string name
        numeric amount "numeric(14,2)"
    }
    STUDENT_INVOICE {
        uuid id PK
        uuid student_id FK "(ext students)"
        uuid academic_year_id FK "(ext structure)"
        string number UK "INV-2026-000123"
        date issue_date
        date due_date
        string status "Draft|Issued|PartiallyPaid|Paid|Void"
    }
    INVOICE_LINE {
        uuid id PK
        uuid student_invoice_id FK
        string description
        numeric amount
        uuid discount_grant_id FK "nullable"
    }
    DISCOUNT_GRANT {
        uuid id PK
        uuid student_id FK "(ext students)"
        string kind "Scholarship|Sibling|Exemption|..."
        string value_type "Percent|Amount"
        numeric value
        uuid approved_by_user_id FK "(ext identity)"
    }
    INSTALLMENT {
        uuid id PK
        uuid student_invoice_id FK
        int sequence
        date due_date
        numeric amount
        string status "Pending|PartiallyPaid|Paid"
    }
    PAYMENT {
        uuid id PK
        uuid student_id FK "(ext students)"
        string reference UK "receipt/reference number"
        numeric amount
        string method "Cash|BankTransfer|Cheque|..."
        date received_date
        string status "Posted|Voided"
        uuid received_by_user_id FK "(ext identity)"
    }
    PAYMENT_ALLOCATION {
        uuid id PK
        uuid payment_id FK
        uuid student_invoice_id FK
        uuid installment_id FK "nullable"
        numeric amount
    }
    REFUND {
        uuid id PK
        uuid payment_id FK
        numeric amount
        string reason
        uuid approved_by_user_id FK "(ext identity)"
        timestamptz refunded_at
    }
```

Key rules and invariants:

- Money is `numeric(14,2)` end-to-end; all write operations are transactional.
- **Posted payments are immutable** — voiding/reversal creates new records, never edits; refunds reference the original payment and require approval.
- Allocations per payment never exceed the payment amount; invoice balance = lines − discounts − allocations; validation is both application-side and DB-checked where possible.
- Overpayments land on a student credit balance (Q3 default) — modeled as an unallocated payment remainder, not negative invoice lines.

## 8. Learning — schema `learning`

```mermaid
erDiagram
    HOMEWORK ||--o{ HOMEWORK_ATTACHMENT : "has"
    HOMEWORK ||--o{ SUBMISSION : "receives"
    SUBMISSION ||--o{ SUBMISSION_ATTACHMENT : "has"
    STUDENT ||--o{ SUBMISSION : "submits (ext)"
    LEARNING_RESOURCE {
        uuid id PK
        uuid subject_id FK "(ext structure, nullable)"
        uuid class_section_id FK "(ext structure, nullable)"
        string title
        string kind "File|Link"
        string url_or_path
    }
    HOMEWORK {
        uuid id PK
        uuid class_section_id FK "(ext structure)"
        uuid subject_id FK "(ext structure)"
        uuid staff_member_id FK "(ext staff)"
        string title
        string instructions_en
        string instructions_ar
        timestamptz due_at
        string status "Draft|Published|Closed"
    }
    SUBMISSION {
        uuid id PK
        uuid homework_id FK
        uuid student_id FK "(ext students)"
        string status "Submitted|Late|Missing|Reviewed"
        timestamptz submitted_at
        string teacher_feedback
    }
```

Key rules: unique (homework_id, student_id); submissions accepted after due date are marked `Late`; teacher feedback stored on the submission.

## 9. Communication — schema `communication`

```mermaid
erDiagram
    ANNOUNCEMENT ||--o{ ANNOUNCEMENT_TARGET : "targets"
    USER ||--o{ NOTIFICATION : "receives (ext identity)"
    USER ||--o{ NOTIFICATION_PREFERENCE : "configures (ext identity)"

    ANNOUNCEMENT {
        uuid id PK
        string title_en
        string title_ar
        string body_en
        string body_ar
        uuid author_user_id FK "(ext identity)"
        timestamptz publish_at
        timestamptz expires_at "nullable"
        string status "Draft|Published|Expired"
    }
    ANNOUNCEMENT_TARGET {
        uuid id PK
        uuid announcement_id FK
        string target_type "All|Role|GradeLevel|Section|User"
        uuid target_id "nullable, interpretation depends on type"
    }
    NOTIFICATION {
        uuid id PK
        uuid user_id FK "(ext identity)"
        string type "Absence|GradePublished|InvoiceIssued|PaymentReceived|Announcement|..."
        string title
        string body
        string link_url "nullable"
        boolean is_read
        timestamptz created_at
    }
    NOTIFICATION_PREFERENCE {
        uuid id PK
        uuid user_id FK "(ext identity)"
        boolean in_app
        boolean email
        boolean sms
    }
    OUTBOX_MESSAGE {
        uuid id PK
        string type "Email|Sms|Push"
        jsonb payload
        string status "Pending|Processing|Sent|Dead"
        int attempts
        timestamptz available_at
        timestamptz processed_at "nullable"
    }
```

Key rules: notifications are written in-app immediately (same transaction as the trigger) while external sends flow through the outbox; read/unread is per notification; preferences gate external channels only.

## 10. Audit — schema `audit`

```mermaid
erDiagram
    AUDIT_LOG {
        uuid id PK
        uuid actor_user_id FK "(ext identity, nullable for system)"
        string actor_display "denormalized snapshot"
        string action "Create|Update|Approve|Publish|Post|Void|Refund|Login|..."
        string module
        string entity_type
        uuid entity_id
        jsonb changes "field-level before/after summary, redacted"
        inet ip_address "nullable"
        string correlation_id
        timestamptz created_at
    }
```

Key rules: append-only; **never contains passwords, tokens, or secrets**; denormalized actor display so logs survive account deactivation; retained per policy (documented in `security-plan.md`).

## 11. Global relationship summary

| Entity | Relates to | Cardinality | Rule |
|---|---|---|---|
| User — Role | via UserRole | n..n | Role codes fixed; user has ≥1 role. |
| Role — Permission | via RolePermission | n..n | Permissions are capability codes. |
| AcademicYear — AcademicTerm | contains | 1..n | One active year; terms ordered. |
| GradeLevel — ClassSection | per year | 1..n | Sections scoped to academic year. |
| StaffMember — TeacherAssignment | assigned | 1..n | Unique per (staff, subject, section, year). |
| ClassSection — TimetableSlot | scheduled | 1..n | No overlapping slots per teacher/room/section. |
| Student — Guardian | via StudentGuardian | n..n | Flags: primary, emergency, pickup. |
| Student — Enrollment | history | 1..n | Append-only; one active per year. |
| AttendanceSession — AttendanceRecord | records | 1..n | Unique (session, student). |
| Assessment — AssessmentComponent | split | 1..n | Weights validated. |
| Grade — GradeChange | trail | 1..n | Append-only; reason required. |
| StudentInvoice — Installment | schedule | 1..n | Sum matches invoice net of discounts. |
| Payment — PaymentAllocation | allocates | 1..n | Σ allocations ≤ payment amount. |
| Refund — Payment | against | n..1 | Requires approval; audited. |
| Homework — Submission | receives | 1..n | Unique (homework, student). |
| User — Notification | receives | 1..n | Read/unread state. |
| (any) — AuditLog | audited as | 1..n | Append-only, redacted. |

## 12. Key design decisions (rationale)

1. **Sections are year-scoped** (`ClassSection.academic_year_id`): avoids the classic bug where timetable/attendance/enrollment for different years collide; cloning a year is an explicit operation.
2. **Enrollment as append-only history**: status transitions close rows, never overwrite; transfers keep a full chain (previous → new).
3. **Publication snapshots** (`TermSubjectResult`, `ReportCard`): issued report cards must not change when a late grade correction happens; corrections produce revised published versions.
4. **Grades at component granularity**: `Grade` references `AssessmentComponent`; simple assessments can have a single implicit component — this keeps the model uniform without special cases.
5. **Ledger-style finance**: payments/refunds/allocations are events; balances are derived; no destructive edits. This gives auditability and reconciliation for free.
6. **DB-level duplicate prevention** (attendance, enrollment, submissions, payment references) — application validation alone is insufficient under concurrency.
7. **Audit as a separate append-only stream**, written by interceptor + explicit events, not by trigger hacks; content rules prevent secret leakage.
8. **Refinements vs. the brief's candidate list**: added `Installment`, `DiscountGrant`, `TermSubjectResult`, `ReportCard`, `AnnouncementTarget`, `NotificationPreference`, `OutboxMessage`, `RefreshToken`, `AdmissionApplication`, `StudentDocument` because the required workflows need them; `School` kept as a singleton settings entity. No candidate entity was dropped without replacement.
