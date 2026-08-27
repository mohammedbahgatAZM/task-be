# Story intake

- Folder: `.squad/stories/communication-channels/CC-2/intake.md`

---

## Feature

- **Feature name (display):** Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CC-2`
- **Work item type:** `Story`

---

## Title

```
WhatsApp
```

---

## Description

```
Role: Customer
As a customer, I want to contact support via WhatsApp, so that I can get help on a channel I already use daily.
```

---

## Acceptance criteria

```
- A WhatsApp message from a customer creates or updates a ticket in real time.
- Agents can send text, images, and documents to the customer via WhatsApp from within the ticket.
- WhatsApp message delivery and read status are visible to the agent.
- WhatsApp conversations comply with the provider's messaging window/template rules.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CC-1 (shared ingestion path), Ticket Management TM-1/TM-5.
- **Depends on code areas or other stories:** backend CC-1's shared ingestion service and `TicketAttachment`.

## Extra notes (optional)

- **No real WhatsApp Business API account exists.** Same stub/mock decision as CC-1: an `IWhatsAppSender` seam records what would have been sent; an inbound webhook-shaped endpoint stands in for the provider's incoming-message callback. "Real time" here means the webhook processes synchronously when called — it cannot mean push-to-customer in real time without a real provider.
- **Delivery/read status** — model a `WhatsAppMessageStatus` (Sent, Delivered, Read, Failed) on each outbound WhatsApp message, updated via a second stub webhook standing in for the provider's status-callback.
- **24-hour messaging window rule** — WhatsApp Business API only allows free-form replies within 24 hours of the customer's last inbound message; outside that window, only pre-approved "template" messages are allowed. Model this as a real domain rule (computed from the last inbound message's timestamp) even though no real provider enforces it yet — reject (or flag) a non-template send attempted outside the window, so the UI has a real signal to show, not a decorative one.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on CC-1's shared ingestion service, `TicketAttachment`/`IAttachmentStorage`.

## Out of scope

- Real WhatsApp Business API integration.
- Template message *management* (creating/approving templates with Meta) — only the window-rule check and an `IsTemplate` flag on send.
