# Story intake

- Folder: `.squad/stories/customer-portal/CP-3/intake.md`

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CP-3`
- **Work item type:** `Story`

---

## Title

```
View history
```

---

## Description

```
Role: Customer
As a customer, I want to view my past tickets and interactions, so that I have a record of my support history.
```

---

## Acceptance criteria

```
- Closed and resolved tickets remain visible and searchable in the portal.
- The customer can reopen a resolved ticket within a configurable time window.
- Attachments and resolution notes remain accessible on past tickets.
- History can be filtered by date range or category.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Story CP-2 (`GET /api/customers/{id}/tickets`, extended with the same `query`/`categoryId`/`from`/`to` filters this AC needs — no new list endpoint).
- **Depends on code areas or other stories:** backend Ticket Management TM-4 (`TicketService.RecordStatusChangeAsync`, reused for the reopen transition), TM-1 (`GetStatusHistoryAsync`, used to find when a ticket became Resolved/Closed), TM-1's existing attachment/timeline endpoints (reused unmodified).

## Extra notes (optional)

- **"Closed and resolved tickets remain visible and searchable"** needs no new endpoint — Story CP-2's `GET /api/customers/{id}/tickets` already returns every status; this story is really "confirm/document that filter doesn't need special-casing," not new backend behavior.
- **Reopen window**: a new `CustomerPortalOptions.ReopenWindowDays` (default 7, same `IOptions<T>`-bound-from-appsettings pattern as `AiFeaturesOptions`). New `POST /api/tickets/{id}/reopen` (customer-facing, takes the caller's `customerId` for the same ownership check as CP-2's portal-reply): rejects (`400`) if the ticket isn't currently `Resolved`/`Closed`, or if the most recent transition *into* that status (from `GetStatusHistoryAsync`, not `ClosedAtUtc` alone — `Resolved` never sets that field) is older than the window. On success, calls `TicketService.RecordStatusChangeAsync(ticketId, TicketStatus.Open, customerName, "System", "Reopened by customer via self-service portal", ct)` — `changedByKind` is `"System"`, not `"Customer"`, because `TicketStatusChangeEntry`'s constructor only accepts `"Agent"`/`"System"` (anything else silently coerces to `"Agent"`, which would misattribute the action) — flagged explicitly as a stand-in until that entity is extended with a real `"Customer"` kind, not a new gap this story introduces silently.
- **"Attachments and resolution notes remain accessible"** — attachments reuse Ticket Management TM-1's existing endpoints unmodified. "Resolution notes" are interpreted as customer-visible messages (`TicketMessage`, not the always-internal `TicketNote` — see `TicketMessage.cs`'s own doc comment), reusing the existing `GET /api/tickets/{id}/timeline` endpoint's `isCustomerVisible` flag — the portal simply never renders `isCustomerVisible: false` entries. No backend change needed for this half either.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- `src/SupportCrm.Application/CustomerPortal/CustomerPortalOptions.cs` (new), extends `CustomerPortalTicketService.cs` (Story CP-2) with `ReopenAsync`.

## Out of scope

- Submitting tickets (CP-1) and tracking/listing (CP-2) — done. FAQ integration (CP-4) and feedback (CP-5) — each is its own story.
- A real `"Customer"` `ChangedByKind` on `TicketStatusChangeEntry` — flagged as a stand-in, not built here.
