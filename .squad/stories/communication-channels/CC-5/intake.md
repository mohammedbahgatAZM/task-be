# Story intake

- Folder: `.squad/stories/communication-channels/CC-5/intake.md`

---

## Feature

- **Feature name (display):** Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CC-5`
- **Work item type:** `Story`

---

## Title

```
Web forms
```

---

## Description

```
Role: Customer
As a customer, I want to submit a support request through a web form, so that I can describe my issue in a structured way.
```

---

## Acceptance criteria

```
- A configurable web form captures required fields (name, contact, category, description, attachments).
- Submitting the form creates a ticket and shows the customer a confirmation with the ticket number.
- Form fields can be customized per category by an administrator.
- Submitted forms are validated before creating the ticket (required fields, file types/size).
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CC-1 (shared ingestion path), Ticket Management TM-1 (`TicketCategory`), CC-1 (`TicketAttachment`).
- **Depends on code areas or other stories:** backend CC-1's ingestion service and `TicketAttachment`, Ticket Management TM-2's `TicketCategory`.

## Extra notes (optional)

- Unlike CC-1..CC-4, this channel needs **no external provider at all** — a web form submission is a normal, direct HTTP request from this app's own frontend. There is no "stub" here; this is a fully real feature, not a mocked integration.
- **"Configurable per category by an administrator"** — model a `WebFormFieldDefinition` per `TicketCategory` (field name, field type — text/textarea/email/phone/file —, whether required, display order). No admin UI existed anywhere in this codebase before this story; this is the first screen requiring basic CRUD management UI (not just read-only lists like TM-2's category dropdown).
- **Validation before ticket creation** — required-field and file type/size checks happen server-side against the resolved category's field definitions, not just client-side; the server is the source of truth even though the client should mirror the same rules for UX.
- Submission still goes through CC-1's shared ingestion path (channel `WebForm`) so it participates in the same dedup-to-existing-open-ticket behavior as every other channel.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on CC-1's ingestion service, `TicketAttachment`, and TM-2's `TicketCategory`.

## Out of scope

- A drag-and-drop form builder UI — field definitions are managed via a straightforward CRUD list/form, not a visual designer.
