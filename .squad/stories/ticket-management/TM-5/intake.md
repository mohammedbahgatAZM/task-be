# Story intake

- Folder: `.squad/stories/ticket-management/TM-5/intake.md`

---

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `TM-5`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Ticket history
```

---

## Description

```
Role: Support Agent
As a support agent, I want to view the complete history and timeline of a ticket, so that I understand everything that has happened before I respond further.
```

---

## Acceptance criteria

```
- Every message, status change, assignment, and note on a ticket appears in one chronological timeline.
- The timeline distinguishes customer-visible messages from internal notes.
- The full history remains accessible after the ticket is closed.
- The history can be exported (e.g. as PDF) for record-keeping or disputes.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** TM-1 (messages/status changes), TM-3 (assignment changes), TM-4 (escalations).
- **Depends on code areas or other stories:** backend TM-1, TM-3, TM-4. Also feeds Customer Management's CM-3 timeline seam (`ICustomerInteractionSource`) via the `TicketInteractionSource` registered in TM-1 — that source only needs a per-customer summary entry per ticket; this story's per-ticket detailed timeline is a separate, more detailed endpoint.

## Extra notes (optional)

- "Every message... note" requires a `TicketMessage` entity distinct from a `TicketNote` (internal) — a customer-visible message vs. an agent-only note, matching the AC's explicit distinction. Status-change and assignment history already exist as their own tables/entries from TM-1/TM-3/TM-4; this story's job is to **merge** all of them into one read-model timeline, not to introduce new source-of-truth tables for those.
- PDF export: no PDF-generation library is referenced anywhere in this codebase, and adding one is a real dependency decision (e.g. QuestPDF, which is free under its Community license for organizations under a revenue threshold, vs. a commercial library). Given this is a browser-based app, the simplest, dependency-free option is a **frontend, browser-native print-to-PDF** view (a printable timeline page + `window.print()`, using print-specific CSS) rather than server-side PDF generation — flag this choice explicitly in the plan as the recommended default, with server-side generation noted as a future upgrade if a literal downloadable-without-printing PDF is required.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on `Ticket` (TM-1), assignment history (TM-3), and escalation records (TM-4).

## Out of scope

- Server-side PDF generation — see the note above; this story's backend work is just exposing the merged timeline data the frontend prints.
