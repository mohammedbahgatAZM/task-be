# Story intake

- Folder: `.squad/stories/customer-portal/CP-2/intake.md`

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CP-2`
- **Work item type:** `Story`

---

## Title

```
Track requests
```

---

## Description

```
Role: Customer
As a customer, I want to track the status of my submitted requests, so that I know what's happening without needing to ask.
```

---

## Acceptance criteria

```
- The portal lists all of the customer's tickets with current status and last update.
- The customer can open a ticket to see the full conversation and add a reply.
- Status changes are reflected in the portal in near real time.
- The customer can filter their tickets by status or date.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Story CP-1 (`CustomerId`-known ticket creation, `TicketChannel.Portal`).
- **Depends on code areas or other stories:** backend Ticket Management TM-5 (`GET /api/tickets/{id}/timeline`, reused unmodified for "full conversation"), `ITicketRepository.GetByCustomerAsync` (already exists, Customer Management CM-1/Ticket Management TM-1).

## Extra notes (optional)

- **New endpoint `GET /api/customers/{id}/tickets`** — list of a new `CustomerTicketSummaryDto` (reference number, subject, status, priority, category, created-at, and a computed `lastUpdatedAtUtc`), with optional `status`/`categoryId`/`from`/`to`/`query` filters (`query` does a simple `Subject`/`Description` substring match) — same query-param-filtering shape as Customer Management's existing `GetTimeline` endpoint, not a new pattern.
- **"Last update"** isn't a stored column — computed as the max of the ticket's own `CreatedAtUtc` and its latest status-change timestamp (`ITicketRepository.GetStatusHistoryAsync`, already exists and is exactly what `TicketService.GetStatusByReferenceAsync` already does for the single-ticket "track by reference" flow — TM-1's own precedent, reused per-ticket here).
- **"Add a reply"** is a new, distinct action from an agent's reply — Ticket Management TM-5/Communication Channels CC-6's `POST /api/tickets/{id}/reply` is the *agent-outbound* dispatcher (it routes a message OUT to the customer via email/SMS/WhatsApp); a customer's portal reply is *inbound*. New endpoint `POST /api/tickets/{id}/portal-reply` adds a `TicketMessage` (`AuthorKind: "Customer"`, `Channel: Portal`) directly via the existing `ITicketMessageRepository.AddMessageAsync` — no dispatch, no channel routing, since the message is already exactly where it needs to be (the ticket's own thread the agent already watches).
- **"Near real time"** is polling, matching every other "live-ish" view in this codebase (Agent Dashboard's ticket list, the chat widgets) — no WebSockets/SignalR.
- **Ownership check**: `portal-reply` and the ticket-list endpoint both require the caller-supplied `customerId` to actually match the ticket's `CustomerId` (for replies) — since there's no real auth, this is enforced by data ownership, not a security boundary; flagged as the same "no real auth" gap as everywhere else in this codebase.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Continue `Application/CustomerPortal/` (Story CP-1): `CustomerPortalTicketService.cs`, `CustomerPortalDtos.cs`. New endpoints on the existing `CustomersController`/`TicketsController`.

## Out of scope

- Submitting new tickets (CP-1, done) and reopening/history search (CP-3), FAQ integration (CP-4), feedback (CP-5) — each is its own story.
- Real-time push (WebSockets) — polling only, matching this codebase's established convention.
