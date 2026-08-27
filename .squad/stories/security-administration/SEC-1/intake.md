# Story intake

- Folder: `.squad/stories/security-administration/SEC-1/intake.md`

---

## Feature

- **Feature name (display):** Security & Administration
- **Feature slug (folder under `plans/`):** `security-administration`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `SEC-1`
- **Work item type:** `Story`

---

## Title

```
Users and roles
```

---

## Description

```
Role: System Administrator
As a system administrator, I want to create user accounts and assign roles, so that access matches each person's responsibilities.
```

---

## Acceptance criteria

```
- An admin can create, deactivate, and delete user accounts.
- Each user is assigned one or more predefined roles (e.g. Agent, Team Lead, Manager, Admin).
- Deactivated accounts immediately lose system access while their historical data is retained.
- Password/authentication policies (complexity, expiry, MFA) are enforceable.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** none new.
- **Depends on code areas or other stories:** none — this is this codebase's first real authentication system.

## Extra notes (optional)

- **This is genuinely new ground for this codebase.** Every prior module (Ticket Management through Reports & Management) was explicitly built on "no real authentication exists" — `AgentContextService`/`CustomerContextService` are client-tracked-identity stand-ins, not accounts. This story introduces the first real `User` (login/password/roles), kept **deliberately separate** from the existing `Agent` entity (the operational identity already used throughout Tickets/Tasks/Assignments) — no FK between them, no migration touching any existing table. Unifying the two is a large follow-up, not this story's job.
- Real password hashing via `Microsoft.AspNetCore.Identity`'s `PasswordHasher<T>` — not a full Identity/EF Identity-stores setup, just the standalone PBKDF2 hasher class, via the lightweight `Microsoft.Extensions.Identity.Core` package (added to `SupportCrm.Application.csproj`, which had no ASP.NET-framework package references at all before this story).
- Real JWT issuance/validation via `Microsoft.AspNetCore.Authentication.JwtBearer` (already referenced in `SupportCrm.Api.csproj`, unused until now, for validation) plus `System.IdentityModel.Tokens.Jwt` (added to `SupportCrm.Application.csproj`, for token *creation* — `JwtBearer`'s own reference to it isn't visible to a project that doesn't reference `JwtBearer` itself).
- Real TOTP MFA (RFC 6238), hand-rolled in ~60 lines using `HMACSHA1` (built into .NET) — genuinely functional with a real authenticator app (Google Authenticator, etc.), not a mock, consistent with this story's own "genuine, not faked" bar (mirrors Reports & Management's real Excel/PDF export, not the AI features' deliberate mock-provider pattern, which exists only because those needed an actual external LLM).
- "Deactivated accounts immediately lose access" is enforced in the JWT validation pipeline itself (`OnTokenValidated`, re-checking `IsActive` from the database on every authenticated request) — not just "blocked at next login." An already-issued token for a deactivated user is rejected on its very next use.
- A default seeded Admin account ships via migration data (a fixed, clearly-flagged dev-only credential) — without one, nobody could ever log in to create the first user. Documented prominently as "change immediately in any real deployment."
- Password expiry blocks login with a distinct, machine-readable reason (`PasswordExpired`) rather than silently failing — the frontend routes this to a forced change-password flow.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#` / .NET 10.
- `src/SupportCrm.Application/CustomerPortal/CustomerPortalOptions.cs` — the `IOptions<T>`-bound-from-config shape this story's `JwtOptions`/`SecurityOptions` follow.

## Out of scope

- Retrofitting `[Authorize]`/permission checks onto the ~15 pre-existing controllers from every prior module (Tickets, Customers, Knowledge Base, SLA, AI, Customer Portal, Reports, ...) — those remain exactly as open as they are today. This story (and SEC-2) builds a genuine, working, enforced authorization system for the **new** Security & Administration endpoints; wiring it across the rest of the app is a large, separate migration this session flags explicitly rather than attempting silently.
- Permissions themselves (Story SEC-2).
