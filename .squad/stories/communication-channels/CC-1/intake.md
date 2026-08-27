# Story intake

- Folder: `.squad/stories/communication-channels/CC-1/intake.md`

---

## Feature

- **Feature name (display):** Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CC-1`
- **Work item type:** `Story`

---

## Title

```
Email
```

---

## Description

```
Role: Customer
As a customer, I want to submit and receive support communication by email, so that I can use the channel I already work in.
```

---

## Acceptance criteria

```
- An email sent to the support address automatically creates or updates a ticket.
- Agent replies sent from the CRM are delivered as emails, preserving the thread/subject line.
- Attachments in inbound and outbound emails are preserved on the ticket.
- Bounced or undeliverable emails are flagged to the agent.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Ticket Management TM-1 (ticket creation, customer resolution), TM-5 (`TicketMessage`, timeline).
- **Depends on code areas or other stories:** backend TM-1 (`TicketCustomerResolver`, `TicketService`), TM-5 (`TicketMessage`, `ITicketMessageRepository`).

## Extra notes (optional)

- **No real mailbox exists.** Per team decision, this story is a **stub/mock adapter**: a webhook-shaped endpoint stands in for where a real provider (e.g. SendGrid Inbound Parse, Mailgun Routes, or a polled IMAP mailbox) would deliver inbound mail, and an `IEmailSender` seam stands in for actually sending outbound mail (it logs/records instead of delivering). Wiring a real provider is a follow-up once credentials exist — flag this explicitly, do not imply real email is sent or received.
- **This story introduces the shared, channel-agnostic ingestion path** other Communication Channels stories (CC-2..CC-5) reuse: given a channel + requester contact + message body, resolve the customer (TM-1's `TicketCustomerResolver`) and find-or-create an **open** ticket for that customer (not just create a new one every time) — this is also the concrete mechanism behind CC-6's "switching channels mid-conversation does not create a duplicate ticket."
- **Ticket-level attachments don't exist yet** — only Customer Management has customer-level attachments (CM-4). This story needs a `TicketAttachment` entity + storage, following CM-4's `IAttachmentStorage` seam pattern (reuse the interface, a separate implementation/table for ticket attachments).
- **Thread continuity** ("preserving the thread/subject line") means outbound replies should carry the same subject and (in a real provider) an `In-Reply-To`/`References` header pointing at the ticket's email thread — model an `EmailThreadId`-like value on the ticket or on the first inbound message; the mock sender just needs to record what it *would* have sent.
- **Bounces** — model a delivery-status entry (e.g. "Bounced") attached to the outbound message, surfaced on the ticket; a stub webhook endpoint stands in for the provider's bounce callback.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on `Ticket`, `TicketMessage`, `TicketCustomerResolver`, `TicketService` (Ticket Management, already implemented in this codebase).
- Follow Customer Management CM-4's `IAttachmentStorage`/`LocalDiskAttachmentStorage` pattern for `TicketAttachment` storage.

## Out of scope

- Real SMTP/IMAP integration, real email delivery, real bounce detection — all seams/stubs per the team decision.
- CC-2..CC-6 (other channels, unified view) — this story only builds the shared ingestion path they'll reuse, plus email's own inbound/outbound/attachment/bounce mechanics.
