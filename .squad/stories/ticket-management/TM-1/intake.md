# Story intake

- Folder: `.squad/stories/ticket-management/TM-1/intake.md`

---

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `TM-1`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Create and track tickets
```

---

## Description

```
Role: Customer / Agent
As a customer or agent, I want to create a ticket from any channel and track its progress, so that my request is not lost and I can see where it stands.
```

---

## Acceptance criteria

```
- A ticket is created with a unique reference number, timestamp, and originating channel.
- Ticket creation is possible manually by an agent and automatically from email/WhatsApp/chat/SMS/web form.
- The requester can view the current status and last update of their ticket.
- Every status change is timestamped and attributed to a user or system rule.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Customer Management CM-1 (`Customer` aggregate) — this story resolves CM-1's previously-deferred assumption that ticket creation links to an existing/new customer profile with no duplicates.
- **Depends on code areas or other stories:** backend CM-1 (`ICustomerRepository`, `CustomerService`, specifically its duplicate-detection capability) and CM-3's `ICustomerInteractionSource` seam (this story should register a `TicketInteractionSource` into it, and should also give CM-1's `ICustomerActivitySummaryProvider` real data instead of its current stub).

## Extra notes (optional)

- No email inbox, WhatsApp Business API, chat widget, or SMS gateway integration exists anywhere in this codebase. "Automatic" creation from those channels cannot be genuinely automatic yet — model it as a single ingestion endpoint/service that accepts a channel + requester contact + message body (as if a future integration adapter had already parsed the inbound message), so real channel adapters can call it later without changing the domain model. Flag this explicitly as a stand-in, not a real integration, in the plan.
- Ticket creation must resolve the requester to a `Customer`: reuse CM-1's duplicate-detection (by name/contact value) to find an existing customer, or create a new one if none matches closely enough. Define the exact matching rule in the plan (e.g., reuse `ContactDetail` value lookup — an exact match on a stored phone/email/WhatsApp value from CM-2 is a much stronger signal than CM-1's name-similarity score; use both).
- No authentication/user-identity system exists yet (same gap as Customer Management). "Attributed to a user or system rule" should accept a client-supplied actor name/kind (`Agent` vs `System`) the same way Customer Management's `ChangedBy` does, flagged the same way.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on the `Customer` aggregate, `SupportCrmDbContext`, and `AddInfrastructure` DI wiring from Customer Management (CM-1..CM-4, already implemented in this codebase under `src/SupportCrm.Domain/Entities/`, `src/SupportCrm.Application/Customers/`, `src/SupportCrm.Infrastructure/`).
- This is the first entity for a new bounded concern (`Tickets`) — mirror Customer Management's folder convention: `src/SupportCrm.Domain/Entities/Ticket*.cs`, `src/SupportCrm.Application/Tickets/`, `src/SupportCrm.Infrastructure/Persistence/Ticket*.cs`, `src/SupportCrm.Api/Controllers/TicketsController.cs`.

## Out of scope

- Building real email/WhatsApp/chat/SMS channel adapters — only the shared ingestion seam they would call.
- Categories/priorities (TM-2), assignment (TM-3), status-transition rules beyond recording changes/escalation (TM-4), and the unified history/export view (TM-5) — each is its own story below.
- Authentication — actor identity is client-supplied per the note above.
