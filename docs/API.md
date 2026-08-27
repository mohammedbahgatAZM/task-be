# SupportCrm Integrations API (INT-1)

This document is the external contract for `api/integrations/v1/*` — the REST API third-party
systems integrate against. It is separate from the rest of this API, which the agent UI itself
calls using a different authentication scheme and is not a stable external contract.

Interactive docs for every controller (including the internal ones) are also available at
`/swagger` in Development.

## Authentication

Two independent schemes exist side by side:

| Scheme | Used by | Header |
|---|---|---|
| JWT bearer | The agent UI's own logged-in session (Security & Administration) | `Authorization: Bearer <token>` |
| API key | External systems calling `api/integrations/v1/*` | `X-Api-Key: <key>` |

An API key is created once by an administrator (`POST /api/admin/api-keys`, requires an
`Integrations:Create` permission on the caller's own JWT session) and is shown **exactly once** in
the creation response — only its hash is stored afterwards, the same way user passwords are
never stored in plaintext. There is no way to retrieve a lost key; revoke it and create a new one.

## Scopes

Every API key is granted one or more scopes at creation time. Each `api/integrations/v1/*`
endpoint requires exactly one scope:

| Scope | Grants |
|---|---|
| `customers.read` | `GET /api/integrations/v1/customers`, `GET /api/integrations/v1/customers/{id}` |
| `customers.write` | `POST /api/integrations/v1/customers` |
| `tickets.read` | `GET /api/integrations/v1/tickets`, `GET /api/integrations/v1/tickets/{id}` |
| `tickets.write` | `POST /api/integrations/v1/tickets` |
| `users.read` | `GET /api/integrations/v1/users` |

A request with a valid key but a missing scope gets `403` (see Error responses below), not `401`.

## Endpoints

| Method | Path | Scope |
|---|---|---|
| GET | `/api/integrations/v1/customers` | `customers.read` |
| GET | `/api/integrations/v1/customers/{id}` | `customers.read` |
| POST | `/api/integrations/v1/customers` | `customers.write` |
| GET | `/api/integrations/v1/tickets` | `tickets.read` |
| GET | `/api/integrations/v1/tickets/{id}` | `tickets.read` |
| POST | `/api/integrations/v1/tickets` | `tickets.write` |
| GET | `/api/integrations/v1/users` | `users.read` |

A ticket created through `POST /api/integrations/v1/tickets` goes through the exact same
pipeline as one created from the agent UI — AI categorization, department routing,
assignment-rule evaluation, and the `ticket.created` webhook all fire identically.

## Rate limits

100 requests per minute per process (fixed window, `IntegrationsApi` policy — this prototype
limits per-server rather than per-key, a documented simplification). A request over the limit
gets `429` immediately (no queueing) with the error envelope below.

## Error responses

Every error from this API — validation, not found, unauthorized, forbidden, rate limited — uses
the same envelope:

```json
{ "error": "A short, human-readable description of what went wrong." }
```

| Status | Meaning |
|---|---|
| 400 | Request failed validation (e.g. missing required field) |
| 401 | Missing or invalid `X-Api-Key` |
| 403 | Valid key, but missing the scope this endpoint requires |
| 404 | The requested resource does not exist |
| 429 | Rate limit exceeded |

## Webhooks (INT-1)

Register a URL to receive a POST for key ticket events. Managed via the JWT-secured
`api/admin/webhooks` endpoints (not part of the API-key-scoped surface above — webhook
subscriptions are an admin configuration, not something an external system self-registers).

**Event types:** `ticket.created`, `ticket.resolved`.

**Delivery:** a POST with a JSON body, `X-Webhook-Event: <event type>`, and
`X-Webhook-Signature: <hex HMAC-SHA256 of the raw body, using the subscription's secret>` for the
receiver to verify authenticity. Delivery is synchronous and attempted once with a 5-second
timeout — there is no automatic retry queue in this prototype. Every attempt (success or failure)
is logged and visible via `GET /api/admin/webhooks/deliveries`; a failed delivery can be manually
retried via `POST /api/admin/webhooks/deliveries/{id}/redeliver`, and also raises an in-app alert
to every supervisor-flagged agent.

## Scope of this prototype

- Rate limiting is per-server, not per-key, and has no configurable override per key.
- Webhook delivery has no background retry/backoff — failures are logged and alertable, retried
  only on deliberate admin action.
- The External API's read/write surface deliberately covers a minimal, stable field set for
  customers/tickets/users — not the full internal DTOs the agent UI itself uses.
