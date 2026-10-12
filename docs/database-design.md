# SchoolOS — Database Design (PostgreSQL)

> **Status:** Phase 1 deliverable (draft v1, 2026-10-12).
> **Companion docs:** `requirements.md`, `architecture.md`, `erd.md` (entity relationships), `security-plan.md`, `implementation-plan.md`.
>
> This document specifies the initial schema: conventions, tables, keys, constraints, indexes, delete/update behaviors, soft-delete/archival policy, concurrency, transaction boundaries, and seeding. Exact DDL is produced as EF Core migrations in Phase 2; this design is the contract they must satisfy.

---

## 1. Conventions

| Topic | Decision |
|---|---|
| Naming | `snake_case`, plural table names (`students.students`), constraints `ux_` / `ix_` / `ck_` / `fk_`, enforced via `EFCore.NamingConventions`. |
| Schemas | One PostgreSQL schema per module: `identity`, `structure`, `students`, `staff`, `attendance`, `academics`, `finance`, `learning`, `communication`, `audit`. Cross-schema FKs are allowed (single database); cross-schema *writes* stay within module ownership (see `architecture.md` §3). |
| Primary keys | `uuid` (UUIDv7, generated in the application via .NET `Guid.CreateVersion7()`): time-sortable, non-enumerable. |
| Instants | `timestamptz`, stored UTC; `created_at` on all business tables, `updated_at` on mutable ones (set by EF `SaveChanges` interceptor — no DB triggers). |
| Calendar dates | `date` for school-local facts (attendance date, due dates) — interpreted in the School timezone. |
| Enums | Fixed statuses: `text` + `CHECK` constraint (readable; new values = migration). Extensible reference data: lookup tables (`subjects`, `grade_levels`). PostgreSQL `ENUM` types avoided (migration pain). |
| Money | `numeric(14,2)` everywhere. Grades: `numeric(7,2)`. Percentages: `numeric(5,2)` with `CHECK (0 <= x <= 100)`. |
| FK behavior | `ON DELETE RESTRICT` by default; `SET NULL` for optional references; **no cascading deletes on core data**. |
| Nulls & uniques | For uniqueness involving nullable columns use `UNIQUE NULLS NOT DISTINCT` (PostgreSQL 15+) or expression indexes (`coalesce`, partial) — never rely on default NULL semantics. |
| Extensions | `btree_gist` (timetable exclusion constraints); `pg_trgm` optional later for name search. |
| Indexing baseline | Every FK column gets an index (PostgreSQL does not index FKs automatically); additional indexes below are justified by query patterns. |

## 2. Identity (`identity`)

| Table | Key columns | Constraints / indexes |
|---|---|---|
| `users` | `id`, `email`, `user_name`, `password_hash`, `security_stamp`, `status` (`Invited\|Active\|Deactivated`), `preferred_language` (`en\|ar`), `last_login_at` | `ux_users_email` (lower-cased `email`), `ux_users_user_name`; `ix_users_status` |
| `roles` | `id`, `code` (unique), `name_en`, `name_ar` | Seeded fixed set of 8 roles |
| `user_roles` | `user_id FK`, `role_id FK` | PK `(user_id, role_id)` |
| `permissions` | `id`, `code` (`module.action`, unique), `module`, `description` | Seeded catalog; `ix_permissions_module` |
| `role_permissions` | `role_id FK`, `permission_id FK` | PK `(role_id, permission_id)` |
| `refresh_tokens` | `id`, `user_id FK`, `token_hash` (unique), `expires_at`, `revoked_at`, `replaced_by_id FK`, `created_by_ip`, `user_agent` | `ix_refresh_tokens_user_active` — partial `(user_id) WHERE revoked_at IS NULL`; rotation chain via `replaced_by_id` |
| `user_tokens` | `user_id FK`, `name`, `value`, `expires_at` | For password-reset / invite tokens (hashed values) |

Notes: ASP.NET Core Identity (`UserManager`/`RoleManager`) is used with these **custom-mapped** tables (no default `AspNet*` schema). Password hashes use Identity's hasher configuration (PBKDF2 default, upgraded over time).

## 3. School & Structure (`structure`)

| Table | Key columns | Constraints / indexes |
|---|---|---|
| `schools` | `id`, `name_en`, `name_ar`, `timezone` (IANA), `default_locale`, `currency_code`, `address`, `logo_path`, `phone`, `email` | Singleton settings row per deployment (Q6) |
| `academic_years` | `id`, `name` (unique, e.g. `2026-2027`), `start_date`, `end_date`, `status` (`Planned\|Active\|Closed`) | `ux_academic_years_single_active` — unique `(status) WHERE status='Active'` ⇒ at most one active year; `ck` end > start |
| `academic_terms` | `id`, `academic_year_id FK`, `name`, `sequence`, `start_date`, `end_date`, `status` | `ux_terms_year_sequence` `(academic_year_id, sequence)`; at most one `Active` per year (partial unique on `(academic_year_id) WHERE status='Active'`); dates within year bounds (app validation) |
| `grade_levels` | `id`, `name_en`, `name_ar`, `sequence` | `ux_grade_levels_sequence` |
| `class_sections` | `id`, `academic_year_id FK`, `grade_level_id FK`, `name`, `capacity`, `homeroom_staff_id FK (nullable)` | `ux_sections_year_grade_name` `(academic_year_id, grade_level_id, name)`; `ix_sections_year` |
| `subjects` | `id`, `code` (unique), `name_en`, `name_ar`, `is_active` | Soft-deactivatable; cannot be deleted while referenced |
| `classrooms` | `id`, `name` (unique), `capacity`, `is_active` | |
| `teacher_assignments` | `id`, `academic_year_id FK`, `staff_member_id FK`, `subject_id FK`, `class_section_id FK` | `ux_teacher_assignments` `(academic_year_id, staff_member_id, subject_id, class_section_id)`; indexes on each FK |
| `timetable_slots` | `id`, `academic_year_id FK`, `academic_term_id FK`, `class_section_id FK`, `subject_id FK`, `staff_member_id FK`, `classroom_id FK`, `day_of_week` (0–6), `start_time`, `end_time` | `ck` end > start; `ux_timetable_exact` `(class_section_id, academic_term_id, day_of_week, start_time)`; indexes per FK |

**Timetable conflict prevention (DB-enforced):**

```sql
-- requires btree_gist; illustrative for teacher double-booking (same for classroom and section)
ALTER TABLE structure.timetable_slots
  ADD CONSTRAINT ex_timetable_staff_overlap
  EXCLUDE USING gist (
    staff_member_id WITH =,
    academic_term_id WITH =,
    day_of_week WITH =,
    int4range(
      (extract(epoch FROM start_time) / 60)::int,
      (extract(epoch FROM end_time) / 60)::int
    ) WITH &&
  );
```

The same pattern is added for `classroom_id` and `class_section_id`. Application-level checks run first for friendly errors; the constraints are the concurrency backstop.

## 4. Students (`students`)

| Table | Key columns | Constraints / indexes |
|---|---|---|
| `students` | `id`, `student_number` (unique, `YYYY-####`), names EN/AR, `date_of_birth`, `gender`, `status` (`Applicant\|Active\|Transferred\|Withdrawn\|Graduated`), `user_id FK (nullable, unique)`, `photo_path`, contact fields | `ix_students_status`; `ix_students_name` on `(lower(last_name_en), lower(first_name_en))`; student number immutable + allocated per year (Q7) |
| `guardians` | `id`, `user_id FK (nullable)`, names EN/AR, `phone`, `alt_phone`, `email`, `occupation`, `address` | `ix_guardians_user`; one user may guard multiple students |
| `student_guardians` | `student_id FK`, `guardian_id FK`, `relationship`, `is_primary`, `is_emergency_contact`, `is_authorized_pickup`, `custody_notes` | PK `(student_id, guardian_id)`; `ux_one_primary_per_student` unique `(student_id) WHERE is_primary` |
| `admission_applications` | `id`, applicant names/dob/gender, `desired_grade_level_id FK`, `academic_year_id FK`, guardian contact fields, `status` (`Submitted\|UnderReview\|Approved\|Rejected\|Enrolled`), `reviewed_by_user_id`, `reviewed_at`, `decision_notes`, `converted_student_id FK (nullable, unique)` | `ix_applications_status_year`; conversion sets `converted_student_id` exactly once |
| `enrollments` | `id`, `student_id FK`, `academic_year_id FK`, `grade_level_id FK`, `class_section_id FK`, `status` (`Enrolled\|Completed\|Transferred\|Withdrawn\|Graduated`), `start_date`, `end_date`, `notes` | **`ux_enrollments_active` unique `(student_id, academic_year_id) WHERE status='Enrolled'`** (one open per year); `ix_enrollments_section_status`; lifecycle managed as below — no overwrites |
| `student_documents` | `id`, `student_id FK`, `document_type`, file metadata (`file_name`, `mime_type`, `size_bytes`, `storage_path`), `verification_status` (`Pending\|Verified\|Rejected`), `verified_by_user_id`, `verified_at`, `notes` | `ix_student_documents_student`; soft delete only (audited) |

Enrollment lifecycle (enforced in the service layer transactionally, tested):

- **Section change (same year):** the open row's `class_section_id` is updated within the same transaction; the full before/after is captured in the audit trail. The invariant "exactly one open row per (student, year)" is preserved.
- **Transfer / withdrawal / graduation:** the open row is closed (`end_date` set, `status` set to the outcome) — never deleted.
- **Academic-year progression:** the previous year's row stays closed (e.g. `Completed`); a new row opens for the new year (same grade level indicates a repeat).
- Historical rows are never overwritten; the chronological chain of rows is the student's enrollment history.

## 5. Staff (`staff`)

| Table | Key columns | Constraints / indexes |
|---|---|---|
| `staff_members` | `id`, `user_id FK (nullable, unique)`, `employee_number` (unique), names EN/AR, `position`, `department`, `hire_date`, `contract_type`, `status` (`Active\|OnLeave\|Terminated`), `phone`, `email`, `emergency_contact`, `notes` | `ix_staff_status`, `ix_staff_department`; workload derived from `teacher_assignments`/`timetable_slots` |
| `staff_attendance_records` | `id`, `staff_member_id FK`, `attendance_date`, `status`, `note` | `ux_staff_attendance` `(staff_member_id, attendance_date)`; included only if enabled (Q4) |

## 6. Attendance (`attendance`)

| Table | Key columns | Constraints / indexes |
|---|---|---|
| `attendance_sessions` | `id`, `class_section_id FK`, `attendance_date`, `session_type` (`Daily\|Period`), `period_number`, `subject_id FK (nullable)`, `taken_by_staff_id FK`, `locked_at` | `ux_attendance_sessions` — `UNIQUE NULLS NOT DISTINCT (class_section_id, attendance_date, session_type, period_number)`; `ck` period_number between 1 and 12; `ix_sessions_date` |
| `attendance_records` | `id`, `attendance_session_id FK`, `student_id FK`, `status` (`Present\|Absent\|Late\|Excused`), `minutes_late`, `reason`, `note`, `recorded_by_staff_id FK`, `corrected_at`, `corrected_by_staff_id`, `correction_reason` | **`ux_attendance_records` unique `(attendance_session_id, student_id)`** (duplicate prevention); `ix_records_student_date` (via join with sessions — denormalized `attendance_date` column is **added** for index efficiency; kept in sync in the same transaction) |

Corrections update the record but always fill `corrected_at/by/reason`; the before/after snapshot goes to `audit.audit_logs`.

## 7. Academics (`academics`)

| Table | Key columns | Constraints / indexes |
|---|---|---|
| `grading_policies` | `id`, `grade_level_id FK (nullable)`, `pass_mark`, `rounding_mode` (`HalfUp\|HalfEven\|Floor`), `display_decimals` (0–2), `name_en`, `name_ar` | `ux_grading_policies_level` — `UNIQUE NULLS NOT DISTINCT (grade_level_id)` (one default + one per level); `ck` pass_mark 0–100 |
| `assessments` | `id`, `academic_term_id FK`, `class_section_id FK`, `subject_id FK`, `staff_member_id FK`, `type`, `title`, `max_marks`, `weight_percent`, `assessment_date`, `status` (`Draft\|Open\|Closed\|Approved\|Published`) | `ix_assessments_section_subject_term`; **weight validation**: Σ weights per (section, subject, term) must equal 100 ± 0.01 before approval (app + tests) |
| `assessment_components` | `id`, `assessment_id FK`, `title`, `max_marks`, `weight_percent`, `sequence` | `ux_components` `(assessment_id, sequence)`; Σ weights per assessment = 100; every assessment gets ≥1 component (single implicit component allowed — keeps grade model uniform) |
| `grades` | `id`, `student_id FK`, `assessment_id FK`, `component_id FK`, `score`, `status` (`Draft\|Submitted\|Approved\|Published`), `entered_by_staff_id FK`, `submitted_at`, `approved_by_user_id FK`, `approved_at`, `published_at` | **`ux_grades` unique `(student_id, component_id)`**; `ck` score 0–max (app-validated against component, tested); indexes `(component_id)`, `(status)` |
| `grade_changes` | `id`, `grade_id FK`, `old_score`, `new_score`, `reason` (required), `changed_by_user_id FK`, `changed_at` | Append-only; `ix_grade_changes_grade` |
| `term_subject_results` | `id`, `student_id FK`, `academic_term_id FK`, `subject_id FK`, `average`, `outcome` (`Pass\|Fail`), `version`, `computed_at` | `ux_term_results` `(student_id, academic_term_id, subject_id, version)`; created at publication; corrections publish `version+1` |
| `report_cards` | `id`, `student_id FK`, `academic_term_id FK`, `overall_average`, `version`, `published_at`, `published_by_user_id FK` | `ux_report_cards` `(student_id, academic_term_id, version)` |

## 8. Finance (`finance`)

| Table | Key columns | Constraints / indexes |
|---|---|---|
| `fee_structures` | `id`, `academic_year_id FK`, `grade_level_id FK`, `fee_type`, `name`, `amount`, `is_active` | `ux_fee_structures` `(academic_year_id, grade_level_id, name)`; `ck` amount > 0 |
| `student_invoices` | `id`, `student_id FK`, `academic_year_id FK`, `number` (unique via sequence, `INV-2026-000123`), `issue_date`, `due_date`, `status` (`Draft\|Issued\|PartiallyPaid\|Paid\|Void`), `void_reason`, `voided_by_user_id`, `voided_at` | `ix_invoices_student_status`, `ix_invoices_status_due`; posted invoices are not deletable |
| `invoice_lines` | `id`, `student_invoice_id FK`, `description`, `amount`, `discount_grant_id FK (nullable)`, `source_fee_structure_id FK (nullable)` | `ck` amount ≥ 0; `ix_invoice_lines_invoice` |
| `discount_grants` | `id`, `student_id FK`, `academic_year_id FK`, `kind` (`Scholarship\|Sibling\|StaffChild\|Exemption\|Other`), `value_type` (`Percent\|Amount`), `value`, `reason`, `approved_by_user_id`, `approved_at`, `starts_on`, `ends_on` | `ck` `(value_type <> 'Percent' OR value <= 100)`; `ix_discounts_student_year` |
| `installments` | `id`, `student_invoice_id FK`, `sequence`, `due_date`, `amount`, `status` (`Pending\|PartiallyPaid\|Paid`) | `ux_installments` `(student_invoice_id, sequence)`; Σ installment amounts = invoice net total (app + tests) |
| `payments` | `id`, `student_id FK`, `reference` (unique), `amount`, `method` (`Cash\|BankTransfer\|Cheque\|Card\|Other`), `received_date`, `status` (`Posted\|Voided`), `void_reason`, `voided_by_user_id`, `voided_at`, `received_by_user_id`, `notes` | `ux_payments_reference`; `ix_payments_student_date`, `ix_payments_date`; `ck` amount > 0 |
| `payment_allocations` | `id`, `payment_id FK`, `student_invoice_id FK`, `installment_id FK (nullable)`, `amount` | `ux_allocations` — `UNIQUE NULLS NOT DISTINCT (payment_id, student_invoice_id, installment_id)`; invariants (Σ per payment ≤ payment; Σ per invoice ≤ balance) enforced in transactions + tests |
| `refunds` | `id`, `payment_id FK`, `amount`, `reason` (required), `method`, `approved_by_user_id`, `refunded_at` | `ck` amount > 0; Σ refunds per payment ≤ payment net (app + tests); `ix_refunds_payment` |

Reporting view (query layer, not a duplicated store): `finance.v_invoice_balances` = Σ lines − Σ discounts − Σ allocations + credits, used by reports and portal queries.

**Immutability rules (enforced in application + tests + audit, not by editing rows):**

- `payments`/`refunds`: never hard-deleted; voiding sets status + reason + actor; allocation rows for a voided payment are removed **inside the same transaction**, with the full allocation snapshot captured in the audit entry.
- Invoice `Void` similarly audited; financial history is reconstructed from the append-only stream (`payments`, `payment_allocations`, `refunds`, `audit`).
- Money operations never `UPDATE` amounts — wrong amounts are corrected by void + re-issue/refund.

## 9. Learning (`learning`)

| Table | Key columns | Constraints / indexes |
|---|---|---|
| `homework` | `id`, `class_section_id FK`, `subject_id FK`, `staff_member_id FK`, `title`, `instructions_en`, `instructions_ar`, `due_at`, `status` (`Draft\|Published\|Closed`) | `ix_homework_section_due`, `ix_homework_section_status` |
| `homework_attachments` | `id`, `homework_id FK`, file metadata | `ix_homework_attachments_homework` |
| `submissions` | `id`, `homework_id FK`, `student_id FK`, `status` (`Submitted\|Late\|Missing\|Reviewed`), `submitted_at`, `teacher_feedback` | **`ux_submissions` `(homework_id, student_id)`**; `ix_submissions_homework_status` |
| `submission_attachments` | `id`, `submission_id FK`, file metadata | |
| `learning_resources` | `id`, `subject_id FK (nullable)`, `class_section_id FK (nullable)`, `title`, `kind` (`File\|Link`), `url_or_path`, `is_active` | `ix_resources_section_subject` |

## 10. Communication (`communication`)

| Table | Key columns | Constraints / indexes |
|---|---|---|
| `announcements` | `id`, `title_en`, `title_ar`, `body_en`, `body_ar`, `author_user_id FK`, `publish_at`, `expires_at`, `status` (`Draft\|Published\|Expired`) | `ix_announcements_status_publish` |
| `announcement_targets` | `id`, `announcement_id FK`, `target_type` (`All\|Role\|GradeLevel\|Section\|User`), `target_id` | `ix_targets_announcement`; `ck` target_id required except for `All` |
| `notifications` | `id`, `user_id FK`, `type`, `title`, `body`, `link_url`, `is_read`, `created_at` | `ix_notifications_user_unread` `(user_id, is_read, created_at DESC)`; keyset pagination for the inbox |
| `notification_preferences` | `id`, `user_id FK (unique)`, `in_app`, `email`, `sms` | Defaults on first use |
| `outbox_messages` | `id`, `type` (`Email\|Sms`), `payload jsonb`, `status` (`Pending\|Processing\|Sent\|Dead`), `attempts`, `last_error`, `available_at`, `processed_at` | `ix_outbox_pending` partial `(available_at) WHERE status='Pending'`; dispatcher uses `FOR UPDATE SKIP LOCKED` |

## 11. Audit (`audit`)

| Table | Key columns | Constraints / indexes |
|---|---|---|
| `audit_logs` | `id`, `actor_user_id FK (nullable)`, `actor_display`, `action`, `module`, `entity_type`, `entity_id`, `changes jsonb` (redacted field-level before/after), `ip_address inet`, `correlation_id`, `created_at` | `ix_audit_entity` `(entity_type, entity_id, created_at DESC)`, `ix_audit_actor` `(actor_user_id, created_at DESC)`, `ix_audit_created`; **append-only** — the application never updates/deletes; retention policy configurable (default: financial/security ≥ 7 years) |

## 12. Cross-cutting policies

### 12.1 Soft delete, archival, immutability

| Class | Tables | Policy |
|---|---|---|
| Immutable (append-only / event) | `grade_changes`, `term_subject_results` (versioned), `report_cards` (versioned), `payments` (void instead of edit), `refunds`, `payment_allocations`, `audit_logs`, `outbox_messages` (state machine only) | No delete; corrections via new records/status transitions with audit. |
| History (append-only lifecycle) | `enrollments`, `attendance_records` (corrections in place + audit), `grades` (lifecycle + change trail) | No delete; state changes audited. |
| Soft-deactivatable | `subjects`, `classrooms`, `fee_structures`, `learning_resources`, `users`/`guardians`/`staff_members`/`students` (status-based) | `is_active`/`status` only; hard delete forbidden while referenced. |
| Soft-deletable with audit | `student_documents`, `homework_attachments`, `submission_attachments` | `deleted_at` + audit entry; files removed from storage in background. |

### 12.2 Concurrency

- Optimistic concurrency via the PostgreSQL **`xmin` system column** mapped as an EF concurrency token on mutable entities (students, sections, timetable slots, assessments, grades during entry, invoices, fee structures). Stale update → 0 rows → HTTP `409 Conflict`.
- Hot sequential numbers (invoice/payment references) use PostgreSQL sequences; gaps are acceptable (documented; Q3).

### 12.3 Transaction boundaries (must be atomic)

| Operation | Transaction contents |
|---|---|
| Enrollment change (section/transfer/withdraw/graduate) | Close/open enrollment rows + student status update + audit + outbox (notification) |
| Year rollover | Per-section batches: promotions/section assignments + status changes + audit |
| Attendance submit / correction | Session lock (optional) + records upsert + audit; correction same-tx |
| Grade publication | Grade statuses + `term_subject_results` + `report_cards` materialization + notifications outbox + audit |
| Invoice generation | Invoice + lines + installments + audit (batch per grade/section) |
| Payment posting | Payment + allocations + invoice status updates + receipt notification + audit |
| Payment void / refund | Status/void fields + allocation removal or refund row + invoice status recompute + audit |
| Discount approval | `discount_grants` approval fields + invoice adjustment (if any) + audit |

Isolation: default `READ COMMITTED`; explicit row locks (`SELECT … FOR UPDATE`) only where a read-modify-write spans rows (invoice balance checks, payment allocation); `SKIP LOCKED` for the outbox dispatcher.

## 13. Seed & demo data strategy

- **Production seed (always):** roles, permission catalog, grading policy default row (values from Q1 defaults), nothing else. Super-admin bootstrap via environment-provided credentials — never a committed password.
- **Development/demo seed (env-gated, e.g. `SEED_DEMO_DATA=true` in non-production):** school profile, academic year + terms, grade levels, sections, subjects, classrooms, sample staff/students/guardians, sample assessments, fee structures, sample invoices with payments. Clearly marked as demo (e.g. `users.email` in a demo domain, student numbers in a reserved range) and documented — **never distinguishable-from-real data must never reach production**. Demo seeding is refused when `ASPNETCORE_ENVIRONMENT=Production`.
- **Test data:** integration tests build their own fixtures per test class (Testcontainers) — no reliance on the dev seed.

## 14. Migration strategy

- EF Core migrations (`dotnet ef migrations add …`) with the full-path dotnet note (see `architecture.md` §9); one migration history.
- Deployments apply migrations via `dotnet ef migrations bundle` (self-contained) — application never calls `EnsureCreated` outside tests.
- Schema additions follow expand → migrate → contract where a change would break running code.

## 15. Index summary (query-pattern driven)

| Query pattern | Supporting index |
|---|---|
| Attendance by student over a period | `attendance_records (student_id, attendance_date)` (denormalized date) |
| Attendance by session roster | `attendance_records (attendance_session_id, student_id)` (unique) |
| Grades by component / by student | `grades (component_id)`, `grades (student_id)` |
| Timetable by section / by teacher | `timetable_slots (class_section_id, academic_term_id)`, `(staff_member_id, academic_term_id)` |
| Student search by name | `students (lower(last_name_en), lower(first_name_en))` (+ optional `pg_trgm` later) |
| Invoice balance lists | `student_invoices (status, due_date)`, `payment_allocations (student_invoice_id)` |
| Payments by day (reports) | `payments (received_date)` |
| Notification inbox | `notifications (user_id, is_read, created_at DESC)` |
| Audit explorer | `audit_logs (entity_type, entity_id, created_at DESC)`, `(actor_user_id, created_at DESC)` |
