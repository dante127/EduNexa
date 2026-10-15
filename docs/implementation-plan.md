# SchoolOS — Implementation Plan

> **Status:** Phase 1 deliverable (draft v1, 2026-10-12).
> **Companion docs:** `requirements.md` (open questions Q#), `architecture.md`, `erd.md`, `database-design.md`, `security-plan.md`.

---

## 1. How this plan works

- Work moves through **milestones M1–M9**, mapped to the brief's Phases 2–7.
- Each milestone has **exit criteria** — commands that must run green and workflows that must be demonstrable. A milestone is not complete until its gate passes.
- Every feature follows the same pipeline: domain/business rules → validation → persistence → API → authorization → UI → tests (never "entity exists, therefore done").
- **Approvals gate**: questions Q1–Q12 (`requirements.md` §6) must be answered before the milestone that depends on them *accepts* its work (defaults keep development moving; defaults are adjustable pre-publication).

## 2. Milestones

### M1 — Foundation (brief Phase 2)

**Goal:** runnable skeleton with auth, DB, tooling, CI, and the first tests.

Work items:

1. Solution scaffold: `SchoolOS.Domain`, `SchoolOS.Application`, `SchoolOS.Infrastructure`, `SchoolOS.Api`, `SchoolOS.Tests` (xUnit); nullable enabled; Directory.Build.props for shared settings; `.editorconfig`; `global.json` pinning the .NET 10 SDK.
2. `docker-compose.yml` with PostgreSQL (dev) — service `db`, named volume, healthcheck; `.env.example`.
3. EF Core + Npgsql wiring; schemas created; **first migration**: identity + school settings + roles/permissions seed.
4. Auth end-to-end: Identity login, JWT issuance, refresh rotation + revocation, logout, password reset (console/file email sink in dev); rate limiting on auth endpoints; SecurityStamp validation on password change.
5. Cross-cutting plumbing: ProblemDetails error contract with stable codes, FluentValidation pipeline, Serilog structured logging + correlation id, health endpoints, OpenAPI.
6. Next.js app scaffold (App Router, TS strict, Tailwind, shadcn/ui base, locale routing `/en` `/ar` with RTL smoke page), typed API client with silent refresh, login screen.
7. Architecture tests (dependency rules) + first integration tests (Testcontainers PostgreSQL): auth flow, error contract, health.
8. CI: GitHub Actions workflow — restore, `dotnet build`, `dotnet test`, frontend `npm ci && npm run build`; vulnerability scans (`dotnet list package --vulnerable`, `npm audit`).

**Exit criteria (commands):**

```powershell
docker compose up -d db
& 'C:\Program Files\dotnet\dotnet.exe' ef database update --project src/SchoolOS.Infrastructure
& 'C:\Program Files\dotnet\dotnet.exe' build
& 'C:\Program Files\dotnet\dotnet.exe' test
npm ci; npm run build --prefix src/web
```

…all green, login → refresh → logout demonstrable in the browser, CI workflow green on `main`.

### M2 — School structure (brief Phase 3a)

Academic years/terms, grade levels, sections, subjects, classrooms, teacher assignments, timetable with **conflict prevention** (DB exclusion constraints + friendly app errors). Admin CRUD UI for each with lists, filters, validation, empty/loading/error states. Authorization policies + tests. Timetable editor with conflict feedback.

**Exit:** seeded demo year configured through the UI; conflict scenarios rejected (teacher/room/section) — proven by integration tests; architecture test for module boundary intact.

### M3 — Students, guardians, enrollment (brief Phase 3b)

Admissions workflow (submit → review → approve/reject → enroll), student profiles with number allocation (`YYYY-####`), guardian links with flags, enrollment lifecycle (section change, transfer, withdraw, graduate) per `database-design.md` §4, documents upload with verification, search/pagination. Audit hooks for all transitions.

**Exit:** end-to-end admission → enrolled student in a section; enrollment invariants tested (one open per year; history preserved); guardian link unique-primary enforced; document upload validated (type/size/path).

### M4 — Staff & assignments (brief Phase 3c)

Staff profiles, employment fields, user linking, assignment management UI, workload view (periods per teacher from timetable). Staff attendance (config-gated, Q4).

**Exit:** staff list + assignment flows usable; workload report matches timetable data (test).

### M5 — Attendance (brief Phase 4a)

Sessions (Daily/Period per config), fast attendance entry for teachers (roster grid), statuses + reasons, correction workflow (role-scoped windows, reasons, audit), duplicate prevention (DB unique + tests), absence reports, recurring-absence scan job → guardian notifications (outbox).

**Exit:** take → correct → report cycle demoed; duplicate insert rejected at DB level; correction audit entries verified; scheduled scan produces notifications in dev.

### M6 — Academics & grading (brief Phase 4b)

Grading policies (Q1), assessments + components with weight validation, grade entry (teacher) with live validation, submission → approval → publication workflow, `term_subject_results` + `report_cards` materialization at publication, audited grade-change flow, report card render (print/PDF, EN/AR, Q10 template).

**Exit:** full teacher→principal→publish cycle demoed; weight-sum validation rejects bad configurations; published snapshot unaffected by later corrections (test); grade-change trail complete.

### M7 — Finance (brief Phase 5)

Fee structures per year+grade; discounts/scholarships with approval; invoice generation (batch per grade/section) + installments; payment posting + allocations + receipt output; void/refund flows; student/guardian balances; finance reports (collections by period, outstanding by student/class/grade).

**Exit:** accounting invariants hold under tests (Σ allocations ≤ payment; installment sums; refund bounds; concurrent posting conflicts handled); void/refund produce complete audit trail; portal balance matches ledger.

### M8 — Portals & communication (brief Phase 6)

Parent portal (children switch, attendance history, grades, timetable, homework, invoices/receipts, announcements), student portal (self dashboards), **parent-child enforcement end-to-end** (isolation test suite), homework module (teacher publish → student submit → feedback), announcements with targeting + bilingual content, notification center + preferences, outbox dispatcher + SMTP provider with EN/AR templates.

**Exit:** isolation suite green (parent A ↔ child B denied with 404 on every portal route); announcement targeting verified per audience; email delivered in dev catcher with correct locale; notification unread/read states correct.

### M9 — Reports & production readiness (brief Phase 7)

All §K reports with filters + exports (XLSX; PDF/print with Arabic/RTL); audit explorer (filter by actor/entity/date); performance pass (index review against slow queries, N+1 audit, list pagination verification); security review against `security-plan.md` §13 checklist; accessibility pass on core flows; backups + deployment docs (compose prod profile, migration bundle, ops runbook); final acceptance run of a full simulated school term.

**Exit:** all checklist items ticked or explicitly deferred with owner; acceptance scenario completed in both languages; deployment instructions executed once from a clean machine profile.

## 3. Sequencing & dependencies

```
M1 ──► M2 ──► M3 ──► M5 ──► M8 ──► M9
              │      ▲
              ├──► M4 ┘ (assignments feed attendance/grades scope)
              ├──► M6 ──► M8
              └──► M7 ──► M9 (finance reports)
```

- M3 is the backbone (students/enrollment) — attendance, academics, finance, and portals all depend on it.
- M5/M6 can proceed in parallel after M3 if needed; M8 depends on both for portal completeness.
- Q-gates: Q1/Q10 before M6 acceptance; Q3 before M7 acceptance; Q4 before M5 acceptance; Q5 before M8 acceptance; Q7/Q8 before M3 acceptance; Q2 before year-rollover work (M3/M9).

## 4. Definition of Done (per feature — from brief §11)

- [ ] Business rules implemented (domain + application layers).
- [ ] Input validation works (client + server + DB constraints where applicable).
- [ ] Server-side authorization enforced (policy + resource scope where relevant).
- [ ] Migrations included; applied on a fresh database successfully.
- [ ] API contracts documented (OpenAPI) and consistent with docs.
- [ ] Frontend workflow functional end-to-end (no placeholder buttons).
- [ ] Loading / empty / error states handled.
- [ ] Relevant tests executed and passing (evidence captured: command + result).
- [ ] No known critical security issue left unaddressed.
- [ ] Docs updated (this plan, ERD/DB design deltas, requirements if scope changed).

## 5. Phase-1 → Phase-2 handoff

Approvals requested before/while M1–M2 proceed (defaults in `requirements.md` §6 are the fallback):

| Needed for | Question(s) |
|---|---|
| M3 acceptance | Q7 (student numbers), Q8 (calendar), Q2 (promotion/rollover policy) |
| M5 acceptance | Q4 (attendance model + correction windows) |
| M6 acceptance | Q1 (grading model), Q10 (report-card template) |
| M7 acceptance | Q3 (installments, late fees, overpayment, discount approvals) |
| M8 acceptance | Q5 (email/SMS providers + which events notify), Q9 (parent↔teacher messaging) |
| Model-wide | Q6 (single school vs branches), Q11 (legacy import), Q12 (currency/numbers) |

Also required for M1 completion: confirmation of repository conventions (branch strategy — recommend trunk-based with short-lived branches + PR review), and whether to enable GitHub Actions CI on the public repo.

## 6. Working agreements (from brief §10, made concrete)

- Inspect before editing; read existing code first; follow established conventions once code exists.
- Small, coherent changes that build and test cleanly; no feature-disabling to pass builds; no test weakening.
- Never claim implemented/tested/verified without captured evidence (command + output).
- Ambiguities affecting financial/academic/security/historical behavior → document + ask before irreversible decisions; minor choices → sensible default + note in PR/commit.
- Keep an implementation checklist up to date in the repo (start: `docs/implementation-plan.md` checkboxes per M-gate, expanded as work items land).
- End of each milestone: report files changed, features completed, commands executed, test results, known issues, next steps.

## 7. Command cheat-sheet (verified environment)

```powershell
# All .NET commands — full path required (PATH dotnet has no SDKs):
& 'C:\Program Files\dotnet\dotnet.exe' build
& 'C:\Program Files\dotnet\dotnet.exe' test
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations add <Name> --project src/SchoolOS.Infrastructure --startup-project src/SchoolOS.Api

# Database (Docker Desktop must be running):
docker compose up -d db

# Frontend:
npm ci --prefix src/web
npm run dev --prefix src/web
npm run build --prefix src/web
```

## 8. Risk register (execution view)

See `requirements.md` §7 for the full register. Top execution risks: grading-model drift (Q1), timetable constraint subtleties (mitigated by DB exclusion tests), finance invariants (dedicated test suite), RTL polish (started in M1, not deferred), scope growth (milestone gates).


## 9. Progress log

| Date | Milestone | Status | Evidence |
|---|---|---|---|
| 2026-10-12 | Phase 1 — planning pack | Done | six docs under `docs/` (commits `b0d5661` …) |
| 2026-10-12 | M1 — Foundation | Done | scaffold + JWT auth + Next.js login flow (EN/AR, RTL); CI green (`257e483`) |
| 2026-10-12 | M2 — School structure (backend) | Done | 7 tables, MediatR + validation pipeline, permission policies, 25/25 tests; CI green (`1ff24d6`) |
| 2026-10-13 | M2 — School structure (admin UI) | Done | `/[locale]/structure` screens (years/terms/grades/subjects/classrooms/sections/settings), full EN/AR; CI green (`db873d3`) |
| 2026-10-13 | M3 — Students & enrollment (backend, part A) | Done | `students` schema (6 tables), admission workflow + enrollment lifecycle, `YYYY-####` allocation via locked counter, 32/32 tests |
| 2026-10-15 | M3 — Documents backend (part B-1) | Done | `student_documents` + local file storage, multipart upload (type/size/path validation), verification workflow, list/download/soft-delete; 40/40 tests |

### Sequencing note (M2 ↔ M4)

Teacher assignments and the timetable editor move to the M4 (staff) block, since both depend on
`StaffMember`. M2 completes with academic years, terms, grade levels, sections, subjects, and
classrooms (backend + admin UI).

### Split note (M3 part A → part B)

M3 part A (this round) delivers the students backend: domain model + EF configurations
(`students` schema), admission applications (submit, review, approve/reject, enroll),
enrollment lifecycle (change section, transfer, withdraw, graduate), guardian management
with single-primary enforcement, student search/paging, and `YYYY-####` number allocation
backed by a row-locked counter.

M3 part B-1 (documents backend) delivers `StudentDocument` (soft delete), the local file
storage abstraction (`IFileStorage` / `LocalFileStorage`, `Storage` config section), multipart
upload validated by type + size with server-generated storage paths, the verification
workflow (Pending to Verified/Rejected), and list/download/soft-delete endpoints (40/40 tests).
Part B-2 (students and admissions admin UI, including the documents tab) follows in a later
round; audit-log writes and the orphan-file sweep remain deferred to the cross-cutting pass in M9.
