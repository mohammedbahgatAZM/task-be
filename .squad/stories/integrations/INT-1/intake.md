# Story intake

- Folder: `.squad/stories/integrations/INT-1/intake.md`

## Feature

- **Feature name (display):** Integrations
- **Feature slug (folder under `plans/`):** `integrations`

## Tracker

- **Tracker type:** `none` · **Work item id:** `INT-1` · **Work item type:** `Story`

## Title

```
APIs
```

## Description

```
Role: Developer / Integrator
As a developer, I want an API to integrate the CRM with other systems, so that I can extend functionality and automate workflows.
```

## Acceptance criteria

```
- A documented REST API is available covering core objects (customers, tickets, users).
- API access is secured via authentication tokens/API keys scoped by permission.
- API rate limits and error responses are clearly documented and consistently applied.
- Webhooks are available to notify external systems of key events (e.g. ticket created/resolved).
```

## Dependencies

- Depends on: none. This story is foundational for the whole feature — `ApiKey`/`ApiKeyAuthenticationHandler` is reused by nothing else in this feature, but its "scoped API key" pattern is the template Stories 55–57's admin endpoints follow for their own JWT-based `[RequirePermission("Integrations", ...)]` checks.

## Extra notes

- The external API (`api/integrations/v1/*`) is a deliberately small, separate contract from the rest of this codebase's API — new `External*Dto` records, not the richer internal `CustomerDto`/`TicketDto`/`AgentDto` — so an internal refactor never breaks an external integration. Reads go straight to the repositories; writes are reused through `CustomerService.CreateAsync`/`TicketService.CreateAsync` so an externally-created ticket still gets AI categorization, department routing, assignment-rule evaluation, and its own `ticket.created` webhook — exactly like one created from the agent UI.
- Two independent authentication schemes now coexist: the existing JWT bearer scheme (agent UI sessions, Security & Administration) and a new `ApiKey` scheme (`X-Api-Key` header, external systems). Neither is the other's default; every existing `[Authorize]` on internal controllers is untouched.
- Rate limiting uses ASP.NET Core's built-in `Microsoft.AspNetCore.RateLimiting` middleware (a fixed window, 100 req/min) rather than a hand-rolled limiter — **documented scope note: per-server, not per-key** (this prototype doesn't spread state across multiple API instances).
- Webhook delivery is real (an actual `HttpClient` POST to whatever URL is registered, with an HMAC-SHA256 `X-Webhook-Signature` header) — not mocked, unlike the Email/SMS/WhatsApp senders — because a webhook target is just an HTTP endpoint, no paid provider account is needed to make it genuinely work. Verified live: registered a webhook against a real local listener and confirmed the delivered payload, `X-Webhook-Event` header, and signature (independently recomputed and matched).
- **Explicit scope boundary:** webhook delivery is synchronous, attempted once, with a 5-second timeout — no background retry-with-backoff queue. A failed delivery is logged (`WebhookDeliveryLog`) and raises an in-app alert to every supervisor-flagged agent (reusing `AgentNotificationService`, not a new mechanism); redelivery is a deliberate admin action (`POST .../deliveries/{id}/redeliver`), not automatic.
- Full contract documented in `backend/docs/API.md` (auth, scopes, rate limits, error envelope, webhook event types/payload/signature) — this is the "documented REST API" the AC asks for, alongside the existing Swagger UI (`/swagger`) which now also shows the `ApiKey` security scheme.

## Technical hints

- `src/SupportCrm.Domain/Entities/ApiKey.cs`, `WebhookSubscription.cs`, `WebhookDeliveryLog.cs` — new entities, the raw secret (key/webhook secret) is only ever returned once, at creation.
- `src/SupportCrm.Api/Security/ApiKeyAuthenticationHandler.cs` — a second `AuthenticationHandler`, registered under scheme name `"ApiKey"`.
- `src/SupportCrm.Api/Program.cs` — one `AddAuthorization` policy per scope (`RequireClaim("scope", ...)`), `AddRateLimiter` with a consistent JSON `OnRejected` handler.

## Out of scope

- Per-key rate limit overrides (every key shares the same server-wide limit).
- Automatic webhook retry with backoff — redelivery is manual.
- OAuth2/client-credentials flows — API keys only.
