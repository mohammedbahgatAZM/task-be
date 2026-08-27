# Story intake

- Folder: `.squad/stories/customer-portal/CP-1/intake.md`

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CP-1`
- **Work item type:** `Story`

---

## Title

```
Submit tickets
```

---

## Description

```
Role: Customer
As a customer, I want to submit a support ticket through a self-service portal, so that I don't have to call or email.
```

---

## Acceptance criteria

```
- A logged-in customer can create a new ticket with category, description, and attachments.
- The customer receives a confirmation with a ticket reference number.
- Required fields are validated before submission.
- The submitted ticket appears immediately in the agent's queue.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** None — first, foundational story in this feature.
- **Depends on code areas or other stories:** backend Ticket Management TM-1 (`TicketService.CreateAsync`, extended not replaced), TM-2 (`TicketCategory`), Customer Management CM-1 (`Customer`, `ICustomerRepository`), AI Features AI-3 (`TicketCategorizationService` — this story's customer-selected category takes precedence over it), Ticket Management TM-1's existing `TicketAttachmentService` (reused for the attachment half).

## Extra notes (optional)

- **"Logged-in customer" is this feature's first new concept — a portal identity, not real authentication.** Same "no auth, client-side actor tracking" pattern this codebase already uses for `AgentContextService` (Agent Dashboard AD-1), but for customers: the portal "logs in" via `Customer.CustomerNumber` lookup (already exists, `ICustomerRepository.GetByCustomerNumberAsync`) — no password, no session token. Add a new `GET /api/customers/by-number/{customerNumber}` endpoint as the lookup. A real login/password/OTP flow is explicitly out of scope.
- **A logged-in customer's `CustomerId` is already known** — extend `CreateTicketRequest` with an optional `CustomerId`; when provided, `TicketService.CreateAsync` uses it directly instead of running `TicketCustomerResolver`'s name/contact-matching (that resolver stays exactly as-is for every other channel that doesn't already know the customer).
- **Category at creation**: extend `CreateTicketRequest` with an optional `CategoryId`. When the customer explicitly picks one, it wins outright over AI Features AI-3's automatic categorization — `TicketService.CreateAsync` skips the AI categorization call entirely in that case (a human's explicit choice is a stronger signal than a keyword-overlap guess). When omitted, AI-3's existing behavior is unchanged.
- **Attachments**: no file can be attached before a ticket exists, so this is a two-step flow at the API level (unavoidable) — create the ticket, then the portal immediately calls Ticket Management TM-1's existing `POST /api/tickets/{id}/attachments`, unmodified. From the customer's point of view it's one "Submit" action; the sequencing is a frontend concern.
- **"Appears immediately in the agent's queue"** needs no backend work — a portal-created ticket is a normal `Ticket` row from the moment it's saved; every existing agent-facing queue/dashboard view already picks it up.
- A new `TicketChannel.Portal` value distinguishes self-service-portal-originated tickets from `WebForm` (anonymous public contact form, Communication Channels CC-5) and `Manual` (agent-entered) in reporting — stored as a string via the existing `HasConversion<string>` mapping, so adding this enum member needs no migration.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- New bounded concern `CustomerPortal` — `src/SupportCrm.Application/CustomerPortal/`, `src/SupportCrm.Domain/Entities/` additions as needed per later stories, `src/SupportCrm.Api/Controllers/` additions to the existing `CustomersController`/`TicketsController` rather than a new controller (these are actions on existing resources).
- `src/SupportCrm.Application/Tickets/TicketService.cs`'s `CreateAsync` — the exact method extended.

## Out of scope

- Real authentication (password, OTP, session tokens, JWT) — a customer-number lookup only.
- Tracking/listing all of a customer's tickets (CP-2), reopening/history (CP-3), FAQ integration (CP-4), and feedback (CP-5) — each is its own story below.
