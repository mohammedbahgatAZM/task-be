# Story 54 — APIs (Story: INT-1)

---

## Prerequisites

None.

---

## Story Goal

A documented, API-key-secured, rate-limited external REST API covering customers/tickets/users, plus real webhook delivery for `ticket.created`/`ticket.resolved`.

---

## Context — Read These Files First

1. `src/SupportCrm.Api/Program.cs` — the existing JWT bearer scheme setup, `[Authorize]`/`AddAuthorization()` calls this story extends rather than replaces.
2. `src/SupportCrm.Application/Tickets/TicketService.cs`, `CreateAsync`/`RecordStatusChangeAsync` — the two lifecycle points webhook dispatch hooks into.
3. `src/SupportCrm.Application/Tickets/AgentNotificationService.cs` — reused as-is for webhook-failure alerts (its own doc comment: "do not add a second, parallel mechanism").

---

## Backend Tasks

### 1 — Domain

**Files:** `src/SupportCrm.Domain/Entities/ApiKey.cs`, `WebhookSubscription.cs`, `WebhookDeliveryLog.cs` — new. `ApiKey.KeyHash`/`WebhookSubscription.Secret` store SHA-256/HMAC secrets; the raw values only ever exist in the creation response DTOs.

**Files:** `src/SupportCrm.Domain/Repositories/IApiKeyRepository.cs`, `IWebhookRepository.cs` — new, standard `GetByIdAsync`/`GetAllAsync`/`AddAsync`/`SaveChangesAsync` shape plus `GetByKeyHashAsync` and `GetActiveForEventAsync`.

### 2 — Application

**File: `src/SupportCrm.Application/Integrations/ApiKeyService.cs`** — `KnownScopes` (`customers.read`, `customers.write`, `tickets.read`, `tickets.write`, `users.read`), `CreateAsync` (generates `sk_<24 random bytes hex>`, stores only its SHA-256 hash), `ValidateAsync(rawKey)` (hash + lookup, `null` for unknown/revoked, updates `LastUsedAtUtc`), `RevokeAsync`.

**File: `src/SupportCrm.Application/Integrations/WebhookEventTypes.cs`** — `TicketCreated = "ticket.created"`, `TicketResolved = "ticket.resolved"`.

**File: `src/SupportCrm.Application/Integrations/WebhookService.cs`** — `CreateAsync` (validates event types against `WebhookEventTypes.All`, generates a hex secret), `DispatchAsync(eventType, payload)` (fans out to every active matching subscription, HMAC-SHA256-signs the JSON body, POSTs via the named `"webhooks"` `HttpClient`, 5s timeout, logs every attempt), `RedeliverAsync`, alerts supervisors via `AgentNotificationService` on any failed delivery.

**File: `src/SupportCrm.Application/Integrations/ExternalApiDtos.cs`, `ExternalApiService.cs`** — the small external contract (`ExternalCustomerDto`, `ExternalTicketDto`, `ExternalUserDto`) and the service backing it; reads hit `ICustomerRepository`/`ITicketRepository`/`IAgentRepository` directly, writes reuse `CustomerService.CreateAsync`/`TicketService.CreateAsync`.

**File: `src/SupportCrm.Application/Tickets/TicketService.cs`** — constructor gains `WebhookService webhookService`; `CreateAsync` dispatches `ticket.created` right before returning; `RecordStatusChangeAsync` dispatches `ticket.resolved` only on the transition *into* `Resolved` (`newStatus == Resolved && oldStatus != Resolved`).

### 3 — Infrastructure

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — `DbSet<ApiKey>`, `DbSet<WebhookSubscription>`, `DbSet<WebhookDeliveryLog>` + their `OnModelCreating` blocks (unique index on `ApiKey.KeyHash`).

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — `services.AddHttpClient("webhooks", c => c.Timeout = TimeSpan.FromSeconds(5));` plus the new repos/services.

Requires `Microsoft.Extensions.Http` package reference on **both** `SupportCrm.Application.csproj` (where `IHttpClientFactory` is consumed) and `SupportCrm.Infrastructure.csproj` (where it's registered) — a plain `Microsoft.NET.Sdk` class library doesn't pull this in the way `Microsoft.NET.Sdk.Web` does.

### 4 — Api

**File: `src/SupportCrm.Api/Security/ApiKeyAuthenticationHandler.cs`** — a second `AuthenticationHandler`, scheme name `"ApiKey"`, reads `X-Api-Key`, builds a `ClaimsPrincipal` with one `"scope"` claim per granted scope. Overrides `HandleChallengeAsync`/`HandleForbiddenAsync` to return this API's standard `{ "error": "..." }` envelope on 401/403 instead of an empty body.

**File: `src/SupportCrm.Api/Security/RateLimitPolicies.cs`** — `IntegrationsApi` policy name constant.

**File: `src/SupportCrm.Api/Program.cs`** — `.AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", ...)` chained onto the existing `AddAuthentication().AddJwtBearer(...)`; `AddAuthorization` gains one policy per `ApiKeyService.KnownScopes` entry; `AddRateLimiter` with a fixed-window `IntegrationsApi` policy (100/min) and a JSON `OnRejected` handler; `AddSwaggerGen` gains an `ApiKey` security definition.

**Files: `src/SupportCrm.Api/Controllers/ApiKeysController.cs`, `WebhooksController.cs`** — JWT-secured, `[RequirePermission("Integrations", "View"/"Create"/"Edit"/"Delete")]`.

**Files: `src/SupportCrm.Api/Controllers/ExternalApi/ExternalCustomersController.cs`, `ExternalTicketsController.cs`, `ExternalUsersController.cs`** — route prefix `api/integrations/v1/*`, `[EnableRateLimiting(RateLimitPolicies.IntegrationsApi)]`, `[Authorize(Policy = "<scope>")]` per action.

### 5 — Seed data

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — `permissionModules` array gains `"Integrations"`; the existing seeding loop generates its 5 `View`/`Create`/`Edit`/`Delete`/`Export` permission rows and grants them all to the seeded Admin role automatically — no new seeding code needed beyond the one array entry.

---

## Edge Cases & Failure Modes

- **A dead/unreachable webhook URL** — the 5s `HttpClient` timeout bounds how long ticket creation/resolution can be delayed; the failure is caught, logged (`Success = false`, the exception message), and never propagates — ticket creation still returns `201`. Verified live against a closed port.
- **An API key with the wrong scope** — `403`, not `401`; the key itself is valid, it just isn't authorized for that operation. Verified live (`customers.read`-only key against `/users` → `403`).
- **No API key at all** — `401` with the standard error envelope, not a bare empty response. Verified live.
- **Rate limit exceeded** — `429` with `{ "error": "rate_limit_exceeded", ... }`, not the framework's default plain-text response.

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` — 0 errors.
2. **Migration:** covered by this feature's single consolidated `AddIntegrations` migration (see Story 57's Verification Steps for the full six-table list).
3. **Live smoke test (all passed):** admin login → create API key → `GET/POST api/integrations/v1/customers` and `/tickets` with the key → `403` on missing scope → `401` on no key → register a webhook against a real local HTTP listener → create a ticket → confirmed the delivered payload, `X-Webhook-Event` header, and `X-Webhook-Signature` (independently recomputed via Node's `crypto.createHmac` and matched exactly) → registered a second webhook against a closed port → confirmed the failure was logged with a real error message and the triggering request still succeeded.

---

## Done Criteria

- [x] `api/integrations/v1/customers`, `/tickets`, `/users` — API-key-secured, scope-checked, rate-limited.
- [x] `api/admin/api-keys`, `api/admin/webhooks` — JWT-secured admin management.
- [x] `ticket.created`/`ticket.resolved` webhooks fire from `TicketService`'s existing lifecycle points, delivered with a verifiable HMAC signature.
- [x] `docs/API.md` documents auth, scopes, rate limits, error format, and the webhook contract.
- [x] `dotnet build SupportCrm.slnx` succeeds, 0 warnings, 0 errors.
