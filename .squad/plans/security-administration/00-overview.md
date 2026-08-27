# security-administration — plan overview

Entry point for the **security-administration** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 45 | [45-story-SEC-1.md](45-story-SEC-1.md) | Users and roles | SEC-1 | — |
| 46 | [46-story-SEC-2.md](46-story-SEC-2.md) | Permissions | SEC-2 | Story 45 |
| 47 | [47-story-SEC-3.md](47-story-SEC-3.md) | Audit logs | SEC-3 | Story 45, Reports & Management Story 40 |
| 48 | [48-story-SEC-4.md](48-story-SEC-4.md) | System configuration | SEC-4 | Story 45, SLA & Automation Story 22 |

## Dependency notes

- Story 45 introduces the whole `SupportCrm.Application.Security` bounded concern and this codebase's **first real authentication system** — `User`, `Role`, password hashing (`Microsoft.AspNetCore.Identity`'s standalone `PasswordHasher<T>`, package already referenced, unused until now), JWT issuance/validation (`Microsoft.AspNetCore.Authentication.JwtBearer`, also already referenced), and a hand-rolled RFC 6238 TOTP implementation for MFA. Zero new NuGet packages.
- Story 46 adds the permission catalog and a `[RequirePermission(module, action)]` action filter, applied to every SEC endpoint from Stories 45–48.
- Story 47's `AuditLoggingActionFilter` is registered **globally** — it captures mutating requests across every prior module's controllers too, without modifying any of them. It also reuses Reports & Management's `IReportExporter` for its own export action.
- Story 48 reuses SLA & Automation's existing business-hours/holiday CRUD unmodified; it only adds the settings the AC names that don't already exist (languages, notification defaults).

## Explicit scope boundary (read before extending this feature)

**None of the ~15 pre-existing controllers from earlier modules (Tickets, Customers, Knowledge Base, SLA & Automation, AI Features, Customer Portal, Reports & Management) are retrofitted with `[Authorize]`/`[RequirePermission]` in this feature.** Every one of those endpoints remains exactly as open as it was before this feature shipped. This is a deliberate, explicitly flagged decision, not an oversight:

1. Every prior module's frontend calls those endpoints with no auth token at all — a blanket retrofit would break six already-shipped, already-verified features in the same change.
2. Deciding the *correct* permission mapping for each of those ~80+ endpoints is a substantial design exercise of its own, not a mechanical add-on to this feature.
3. The ACs for SEC-1..4 describe a working, enforced permission *system* — demonstrated end-to-end on this feature's own admin endpoints — not an app-wide security audit.

Securing the rest of the app is a natural, sizeable follow-up ("also lock down the existing endpoints"), not something this feature does silently.
