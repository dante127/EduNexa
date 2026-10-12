# SchoolOS — Security Plan

> **Status:** Phase 1 deliverable (draft v1, 2026-10-12).
> **Companion docs:** `requirements.md`, `architecture.md`, `erd.md`, `database-design.md`, `implementation-plan.md`.

---

## 1. Principles

1. **The server is the security boundary.** Frontend checks are affordances only; every endpoint enforces authentication + authorization + scope server-side.
2. **Treat all input as untrusted** — request bodies, query strings, route values, headers, file uploads, and webhook-like payloads.
3. **Least privilege**: roles grant the minimum permissions; portal users see only their own data; staff see only what their role needs.
4. **Defense in depth**: validation + authorization + DB constraints + audit, not any single layer.
5. **Fail safely**: denial is explicit (`403`/`404`), errors leak nothing, sensitive operations are logged.

## 2. Authentication

| Topic | Decision |
|---|---|
| Credential store | ASP.NET Core Identity (`PasswordHasher`, PBKDF2) with custom-mapped tables (see `database-design.md` §2). No custom password crypto. |
| Password policy | Min 10 chars; characters/breach checks configurable; reuse of passwords on reset blocked; hasher upgrade path enabled. |
| Login flow | Email/username + password → **short-lived access JWT (~15 min)** + **rotating refresh token** (opaque, hashed at rest, 256-bit random). |
| Refresh token transport | httpOnly + Secure + SameSite=Lax cookie, path-scoped to the refresh endpoint. Rotation on every use; **reuse detection revokes the whole token family** (suspected theft). |
| Logout | Revokes the presented refresh token; "logout everywhere" revokes all user tokens; password change/reset revokes all sessions. |
| Lockout / throttling | Progressive delay + temporary lockout after repeated failures; all failures audited (no user enumeration in messages). |
| Password reset | Single-use, time-limited (≤1 h), hashed token sent by email; generic response regardless of account existence; all sessions revoked on success. |
| Account lifecycle | Invited → Active → Deactivated (never hard-deleted); deactivation revokes tokens immediately; invites expire. |
| MFA | Out of scope v1; design leaves room (TOTP later) — no changes required to token architecture. |
| Session hardening | Access token kept in memory only (never localStorage); refresh only via the httpOnly cookie; clocks enforced on token validation (no tolerance beyond default). |

CSRF: the refresh endpoint accepts the cookie — protected by SameSite=Lax + an antiforgery token bound to the session (`/csrf` bootstrap endpoint). All state-changing endpoints authenticate via the Authorization header (not cookies), so classic CSRF does not apply to them.

## 3. Authorization

- **Model:** roles (8 fixed) → permission codes → policies. Permission codes are module-scoped verbs: `students.view`, `students.manage`, `attendance.take`, `attendance.correct`, `grades.enter`, `grades.approve`, `grades.publish`, `finance.invoices.manage`, `finance.payments.post`, `finance.refunds.approve`, `reports.view`, `admin.users.manage`, etc. (full catalog seeded in DB).
- **Enforcement:** ASP.NET Core authorization policies on every endpoint (`[Authorize(Policy = …)]`); a fallback policy denies any endpoint without explicit policy. No anonymous endpoints except: login, refresh, password-reset request/confirm, and the public admission inquiry (rate-limited).
- **Resource-based checks** (the cases policies can't express):
  - **Parent ↔ child**: the authenticated guardian must have an active `student_guardians` link to the target student — verified **per request** for every portal route (`/parent/children/{studentId}/**`), not just in lists.
  - **Student self**: portal routes resolve the student from the token identity only; no arbitrary id acceptance.
  - **Teacher scope**: teachers take attendance / enter grades only for sections and subjects where they have a `teacher_assignment`; enforced in handlers via a scope service.
  - **Staff field scope**: accountant sees finance fields; teachers see student academic basics, not fee/guardian/contact details beyond what their UI needs.
- **IDOR prevention pattern:** handlers never trust client-supplied IDs; they resolve the scope from the token identity and intersect it with requested IDs; non-intersecting IDs return `404` (not `403`) to avoid existence leaks on portal routes.
- **UI vs server:** menu/router guards mirror permissions for UX; server enforcement is independent and test-verified.

## 4. Input validation & safe errors

- FluentValidation validators on all commands/queries with client input; domain invariants re-checked in domain methods; DB constraints as backstop.
- Model binding: strict types; no mass-assignment (explicit DTOs only).
- Output: DTO-only responses; never serialize entities; React escapes by default; no `dangerouslySetInnerHTML` for user content (announcements are plain text/markdown-safe subset).
- Errors: RFC 9457 `ProblemDetails` with stable codes; no stack traces, SQL, or internal paths to clients; detailed context goes to logs only.

## 5. Transport, headers, cookies

| Control | Setting |
|---|---|
| HTTPS | Enforced (`UseHttpsRedirection` + HSTS in non-dev). |
| Cookies | `Secure`, `HttpOnly`, `SameSite=Lax`; no cross-site cookies; short path scope. |
| CORS | Explicit allowlist of the frontend origin(s) from configuration; credentials enabled only for the refresh cookie; no wildcard. |
| Security headers | `X-Content-Type-Options: nosniff`, `Referrer-Policy`, frame denial (`X-Frame-Options`/frame-ancestors), restrictive `Content-Security-Policy` served by the frontend (Next.js), `Permissions-Policy` minimal. |
| Rate limiting | ASP.NET Core rate limiter policies: login (per IP+account), password reset (per account+IP), uploads, public admission form; `429` with `Retry-After`. |

## 6. Secrets & configuration

- **No secrets in the repository, ever** — enforced by review + `.gitignore` + secret scanning in CI (e.g. gitleaks) from Phase 2.
- Development: `dotnet user-secrets`; runtime: environment variables (docker compose `.env` files are git-ignored; `.env.example` documents keys).
- JWT signing keys, DB credentials, SMTP credentials, storage keys: unique per environment, rotatable; production managed via the platform's secret store (documented in Phase 7 deployment docs).
- Logs never contain: passwords, tokens, connection strings, full request bodies of auth endpoints.

## 7. File uploads (documents, homework, submissions, photos)

- Allowlist of MIME types + extensions (pdf, common images, office docs as configured); size caps per purpose (config).
- Content sniffing (magic bytes) in addition to the declared type; stored **outside the web root** with randomized storage names (UUID-based), original name kept as metadata only.
- Path traversal blocked (no client-controlled paths).
- Downloads served through an authorized endpoint that re-checks permissions/scope (never direct static file URLs for student documents); `Content-Disposition` attachment with safe filename.
- Virus-scanning hook interface in place (binding to a scanner optional per deployment).

## 8. Database access & SQL injection

- EF Core with parameterized queries everywhere; **no unsafe dynamic SQL**.
- Raw SQL (rare, reporting) uses `FromSqlInterpolated`/parameters — string concatenation forbidden; reviewed case by case.
- DB user for the app is not superuser; migrations run with a separate, more privileged connection only at deploy time.
- Constraints duplicate critical validation (uniques, checks) so bugs can't corrupt data.

## 9. Audit logging (security-relevant)

| Event family | Recorded |
|---|---|
| Auth | login success/failure, lockout, refresh reuse detection, logout-all, password reset request/complete, invite sent/accepted |
| Authorization anomalies | repeated 403s (rate-limited logging) |
| Admin | user created/deactivated, role changes, permission changes, school settings changes |
| Academic | assessment approval, grade submission/approval/publication, grade changes (before/after, reason, actor) |
| Finance | invoice issue/void, payment post/void, allocation changes, discount approval, refund |
| Data | student transfers/withdrawals, enrollment changes, document verification |

Content rules: actor (with denormalized display), timestamp, entity + id, field-level redacted diff, correlation id, IP. **Never**: passwords, tokens, secrets, full payment instrument data (none stored), guardian contact details outside change-diff needs.

Integrity: `audit.audit_logs` is append-only (application-level guarantee + DB role privileges; no update/delete paths in code). Retention configurable (default: security/financial ≥ 7 years, others ≥ 3 years).

## 10. Data minimization & PII

- Portal and list endpoints return only fields the role needs (dedicated DTOs per audience: staff vs parent vs student).
- Student/guardian contact data is not exposed to teachers beyond classroom-need fields.
- Exports (Phase 7) are permission-gated and audited; bulk export limited to admin/finance roles.
- Error messages don't echo PII; logs redact email/phone in generic request logs (kept only where business-required, e.g. audit diffs).

## 11. Dependencies & supply chain

- Pinned, verified package versions (NuGet + npm lockfile); update cadence with changelog review.
- CI runs `dotnet list package --vulnerable --include-transitive` and `npm audit` (fail on high/critical), plus build + tests.
- No unvetted third-party scripts in the frontend; no remote scripts in report/print templates.

## 12. Security testing (executed, not assumed)

Mapped to the brief's testing strategy:

| Test class | Coverage |
|---|---|
| Authorization matrix | Every endpoint × role: allowed/denied expectations, incl. privilege boundaries (teacher ≠ principal actions) |
| Data isolation | Parent A cannot read/modify Parent B's child (all portal routes, incl. direct-ID attempts → 404); student self-scope; teacher scope outside assignments |
| Auth flows | Login/lockout/refresh rotation/reuse revocation/reset single-use; token expiry behavior |
| Validation & errors | Malformed input, oversized uploads, wrong MIME, injection payloads in text fields (stored and echoed safely) |
| Finance invariants | Allocation overflow rejected; void/refund math; concurrent payment posting |
| DB constraints | Duplicate attendance/enrollment/submission rejected at DB level, not just app |
| Rate limits | Login/reset throttling behavior under bursts |

## 13. Production hardening checklist (Phase 7 gate)

- [ ] All above controls implemented and verified by tests/manual checks.
- [ ] Security headers verified on deployed frontend + API.
- [ ] Secrets in platform store; rotation tested once.
- [ ] HTTPS enforced end-to-end; HSTS on.
- [ ] DB backups + restore drill documented and performed.
- [ ] Audit log spot-check: no secrets present, entries complete for the matrix of events.
- [ ] Dependency vulnerability scan clean (no high/critical).
- [ ] Rate limits and lockouts verified in a staging environment.
- [ ] Log redaction verified on sampled logs.
- [ ] File storage directory not web-accessible; download authorization verified.
