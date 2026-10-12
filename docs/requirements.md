# SchoolOS — Requirements

> **Status:** Phase 1 deliverable (draft v1, 2026-10-12).
> **Companion docs:** `architecture.md`, `erd.md`, `database-design.md`, `security-plan.md`, `implementation-plan.md`.
> Open questions referenced as **(Q#)** are listed in §6 and should be approved by the product owner before the affected implementation phase.

---

## 1. Product summary

SchoolOS is a multi-role web application that runs the day-to-day operations of a single school: academic structure, students and enrollment, staff, attendance, assessments and grades, tuition and payments, homework, announcements/notifications, reporting, and audit. The UI is available in **English and Arabic** with full RTL support.

The system must not be a superficial CRUD app: every module ships with its real workflow, business rules, server-side authorization, and tests (see Definition of Done, `implementation-plan.md`).

The first release is delivered in phases; this document defines **what** is built, not how.

## 2. Scope

**In scope (v1)**

- Functional modules A–L of the product brief (§4 below).
- Web only, responsive (desktop-first; tablet/mobile usable for portals and attendance).
- Single school per deployment (Q6); one active academic year at a time.
- In-app notification center + email notifications (replaceable provider); SMS behind an interface only.
- Printable/exportable reports (PDF/print, XLSX) with Arabic/RTL support.

**Out of scope (v1)** — explicit non-goals

- Native mobile apps.
- Real payment-gateway integration (a clean seam is provided; see Q3).
- Payroll, transportation, cafeteria, library, inventory.
- Ministry/state reporting integrations.
- Multi-school tenancy (single school assumed; see Q6).
- Two-factor authentication (documented future enhancement).
- Data migration from a legacy system (Q11).

## 3. Roles

| Role | Primary goals |
|---|---|
| Super Admin | Platform-level administration (deployment setup, security config, user bootstrap). |
| School Administrator | School setup, users & roles, academic configuration, oversight of all modules. |
| Principal | Academic oversight: approvals of assessments/grades, attendance policy, reports. |
| Teacher | Timetable, attendance for own sessions, homework, grade entry for own classes. |
| Accountant | Fee structures, invoices, payments, refunds (per policy), financial reports. |
| Receptionist | Admissions intake, student/guardian records, front-desk inquiries. |
| Parent | Portal for own children: attendance, grades, tuition, announcements. |
| Student | Portal for own record: timetable, homework, grades, announcements. |

Authorization is **server-side** and permission-based, layered over these roles (see `security-plan.md`).

## 4. Functional requirements

### 4.1 Identity & Access (brief §A)

- Secure login/logout; password reset; account lifecycle (invite → activate → deactivate; no hard delete).
- Role-based + permission-based access control; 8 roles above; permissions per module capability.
- Students/parents are restricted to their own records; every parent request re-verifies the parent↔child relationship.
- Security-sensitive operations (logins, failed attempts, resets, role changes, account status changes) are audited.

### 4.2 School Structure (brief §B)

- Academic years (planned → active → closed) and terms; exactly one active year.
- Grade levels → class sections **per academic year** (capacity, homeroom teacher).
- Subjects and curriculum catalog; classrooms and facilities.
- Timetable slots per section/term; the system rejects conflicts involving teacher, classroom, or section double-booking.
- Teacher assignments (staff × subject × section × year) drive workload and timetable eligibility.

### 4.3 Students — admissions, enrollment, records (brief §C)

- Admission application workflow: submitted → under review → approved → enrolled, or rejected/withdrawn; handled by Receptionist/Admin.
- Unique, human-readable student number (Q7).
- Student profile (names EN/AR, DOB, gender, contacts, photo) + documents with verification status.
- Guardians: many-to-many with relationship, primary/emergency/authorized-pickup flags, custody notes.
- Enrollment per academic year with section; section changes, transfers, withdrawals, graduation are recorded as new history — **historical enrollment records are never overwritten**; at most one active enrollment per (student, year).
- Academic history across years; controlled correction workflows for record fixes.

### 4.4 Staff (brief §D)

- Staff profiles and employment information (position, department, hire date, contract type, status).
- Link between User account and StaffMember.
- Teacher-subject/section assignments; teaching schedule; workload views.
- Staff attendance where appropriate (Q4).

### 4.5 Attendance (brief §E)

- Attendance sessions: daily (homeroom) and/or per-period (configurable; Q4).
- Statuses: present / absent / late / excused, with reason and notes.
- **No duplicate attendance records for the same student and session** (enforced in DB + app).
- Correction workflow: role-scoped windows, mandatory reason, fully audited.
- Absence reports; recurring-absence detection triggers guardian notifications.

### 4.6 Academics — assessments, grades, report cards (brief §F)

- Configurable grading policies (per school / grade level): pass mark, weighting rules, rounding (Q1). Weight/max-mark consistency is validated.
- Assessments per subject/section/term with components (e.g. quizzes, exams) carrying max marks and weights.
- Grade entry by responsible teacher; lifecycle: draft → submitted → approved (principal/admin) → published.
- Grade changes after publication go through an audited correction flow with reason; **every grade change records who made it and why**.
- Subject averages, term results, final results; published results are materialized as immutable snapshots so later edits never silently alter issued documents.
- Report cards and transcripts (print/PDF, EN/AR).

### 4.7 Finance — tuition (brief §G)

- Fee structures per academic year and grade (per-item fees); per-student adjustments via scholarships/discounts/exemptions with approval.
- Invoices, additional charges, installment schedules (Q3), partial/full payments, receipts, outstanding balances.
- Payments and receipts are **immutable once posted**: void/reversal and refunds instead of edits/deletes, all audited.
- Payment allocation across invoices/installments with reconciliation; overpayment handling (credit balance; Q3).
- Monetary values use decimal types end-to-end; all financial operations are transactional.
- Financial reports: collections by period, outstanding balances by student/class/grade.
- No real payment gateway in v1.

### 4.8 Learning — homework (brief §H)

- Homework assignments (instructions EN/AR, attachments, deadlines) per section/subject.
- Student submissions with status (submitted / late / missing) and teacher feedback.
- Learning resources and links, scoped to a section/subject.

### 4.9 Parent & Student portals (brief §I)

- Role-specific dashboards: attendance history, grades & report cards, timetable, homework & deadlines, invoices/receipts/balance, announcements & notifications.
- A parent may have multiple children; **the backend verifies the parent-child relationship on every protected request** and scopes data accordingly.

### 4.10 Communication & notifications (brief §J)

- School announcements with audience targeting (all / role / grade / section), bilingual content, publish windows.
- In-app notification center with read/unread states; per-user notification preferences.
- Email via replaceable provider; SMS behind an interface. Notification dispatch is asynchronous (outbox) and **never blocks core academic/financial transactions**.

### 4.11 Reports (brief §K)

Searchable, filterable, paginated reports with export:

- Student enrollment; daily & monthly attendance; student academic performance; class & subject results; fee collection & outstanding balances; teacher workload; transfers & withdrawals.

Arabic text, RTL formatting, and printable layouts are supported in all reports.

### 4.12 Audit & administration (brief §L)

- Audit of important create/update/approval/financial/security operations: actor, timestamp, entity, change summary, correlation id.
- **Secrets, passwords, and tokens are never written to audit logs.**
- Admin: manage users, roles, permissions, school settings (name, timezone, locale), academic configuration; controlled correction workflows.

## 5. Non-functional requirements

| Area | Requirement |
|---|---|
| Internationalization | EN + AR UI, full RTL; localized dates/numbers; bilingual content fields for user-authored content (announcements, homework); RTL-capable report/PDF layouts. |
| Accessibility | WCAG 2.1 AA target for core flows; keyboard navigation; sufficient contrast; accessible forms with error messaging. |
| Performance | Paginated lists everywhere; no unbounded collections; p95 target < 500 ms for typical API reads on seeded realistic data; heavy reports paginated and index-backed; avoid N+1 queries. |
| Security | See `security-plan.md`: server-side authorization, validation of all input, rate limiting, secure cookies/CORS, no secrets in repo/logs, audit logging. |
| Data integrity | Financial and grade-critical operations transactional; optimistic concurrency on editable records; DB constraints in addition to application validation; UTC storage + school timezone setting. |
| Reliability | Notification failures never corrupt core flows (outbox + retries); health endpoint; documented backups (Phase 7). |
| Maintainability | Modular monolith, migrations, automated tests per brief §8; CI-ready build/test commands. |
| Observability | Structured logs with correlation IDs; audit trail for business changes. |

## 6. Open business questions (product-owner approval required)

Each question has a **proposed default** so work can proceed if no answer arrives; the default is documented in the affected deliverable and can be changed later with a migration/report adjustment.

| # | Question | Proposed default | Impacts |
|---|---|---|---|
| Q1 | Exact grading model: weights, max marks, pass threshold, rounding, retakes? | Percentage-based: subject-term total = weighted sum of assessments (weights sum to 100), pass = 50%, round half-up at display, 2 decimals stored. | Phase 4 |
| Q2 | Promotion & year rollover: who is promoted, what happens to unpaid balances and open attendance corrections? | Manual bulk promotion from section lists at year close; balances carried into the new year; open corrections must be closed before year close. | Phases 3/5 |
| Q3 | Fee policy defaults: number of installments, late fees, overpayment handling, discount approval levels? | 3 installments per year (configurable); no late fees in v1; overpayment → student credit; discounts require Accountant + Admin approval. | Phase 5 |
| Q4 | Attendance model: daily only, per-period only, or both? Correction windows? Staff attendance needed? | Both modes supported, schools configure; teacher corrections within 48h with reason, Admin anytime; staff attendance included but optional-per-config. | Phase 4 |
| Q5 | Notification channels & provider: which SMTP/email, which SMS vendor (later), which events notify? | SMTP via config; SMS provider interface only; events: absence, grade publication, invoice issued, payment received, announcement. | Phase 6 |
| Q6 | Single school per deployment, or multi-branch now? | Single school; data model avoids structural blocks for later multi-branch. | Model-wide |
| Q7 | Student number format. | `YYYY-####` (e.g. 2026-0042), sequential per academic year, immutable. | Phase 3 |
| Q8 | Academic calendar: term count and school week. | 2 semesters default (configurable), week = Sun–Thu (configurable). | Phase 3 |
| Q9 | Parent ↔ teacher direct messaging in v1? | Not in v1; announcements/notifications only. | Phase 6 |
| Q10 | Official report-card template (layout, branding, fields, signatures). | Generic configurable template with school header; real template provided before Phase 4 acceptance. | Phase 4 |
| Q11 | Legacy data import needed? | None assumed; import tooling excluded from v1. | Phase 3+ |
| Q12 | Default currency and numeral style. | Single currency from School settings (configurable); Western digits with Arabic text. | Phase 5 |

## 7. Risks

| # | Risk | Impact | Mitigation |
|---|---|---|---|
| R1 | Grading model ambiguity (Q1) | Wrong calculations baked into report cards | Default policy + policy engine configurable; confirm before Phase 4 acceptance. |
| R2 | Timetable conflict logic is easy to get subtly wrong | Double-bookings | DB-level exclusion constraints + dedicated conflict tests. |
| R3 | Finance correctness (allocations, refunds, balances) | Money errors, trust loss | Immutable ledger-style records, transactional ops, invariant tests. |
| R4 | RTL/Arabic polish takes longer than expected | UX quality | Establish i18n skeleton + RTL component pass in Phase 2, not at the end. |
| R5 | Very large scope | Slippage | Milestone gates with exit criteria; strict DoD; no feature marked complete with partial workflow. |
| R6 | Local dev relies on Docker (daemon initially off) | Friction | Docker Compose for PostgreSQL; documented start steps. |
| R7 | No CI yet on the repository | Regressions | Add GitHub Actions build/test pipeline in Phase 2. |

## 8. Dependencies

- Product brief (authoritative) — mapped across these docs.
- Stack: ASP.NET Core on .NET 10, EF Core + PostgreSQL, Next.js + TypeScript + Tailwind + shadcn/ui (see `architecture.md`).
- Environment: Windows dev machine, .NET SDK 10.0.101, Node 22, Docker (PostgreSQL container), git + GitHub repo `dante127/EduNexa`.
- External services (replaceable): SMTP for email; SMS provider (interface only in v1).

## 9. Success criteria

- Every phase passes its gate in `implementation-plan.md` (build + tests green, workflow demoable end-to-end).
- Each feature meets the Definition of Done (brief §11): business rules, validation, server-side authorization, migrations, API contracts, functional UI, states, executed tests, no known critical security issue, docs updated.
- A full school year can be operated in the system: setup → enrollment → timetable → attendance → grades → invoices/payments → portals → reports — in both English and Arabic — with no placeholder interactions in completed features.
