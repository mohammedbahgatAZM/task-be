# communication-channels — plan overview

Entry point for the **communication-channels** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 10 | [10-story-CC-1.md](10-story-CC-1.md) | Email | CC-1 | Ticket Management Stories 05–09 |
| 11 | [11-story-CC-2.md](11-story-CC-2.md) | WhatsApp | CC-2 | Story 10 |
| 12 | [12-story-CC-3.md](12-story-CC-3.md) | Live chat | CC-3 | Story 10, Ticket Management Story 07 |
| 13 | [13-story-CC-4.md](13-story-CC-4.md) | SMS | CC-4 | Story 10, Ticket Management Story 08 |
| 14 | [14-story-CC-5.md](14-story-CC-5.md) | Web forms | CC-5 | Story 10, Ticket Management Story 06 |
| 15 | [15-story-CC-6.md](15-story-CC-6.md) | Unified conversation view | CC-6 | Stories 10–14, Ticket Management Story 09 |

## Dependency notes

- Story 10 is foundational for this feature: it introduces `TicketIngestionService` (the shared, channel-agnostic dedup-to-open-ticket path), `TicketMessage.Channel`, `TicketAttachment`, and `TicketMessageDeliveryStatus` — every other story in this feature reuses at least one of these rather than rebuilding it.
- Stories 11–14 (WhatsApp, Live chat, SMS, Web forms) are largely independent of each other once Story 10 lands — they can be planned/implemented in any order relative to one another, only Story 15 needs all four.
- **Explicit, team-approved scope decision across this entire feature:** no real email/WhatsApp/SMS provider account exists. `IEmailSender`, `IWhatsAppSender`, `ISmsSender` are mock/logging seams (Stories 10, 11, 13) — they record what would have been sent, they do not deliver real messages. Wiring real providers is a future feature, not implied by any story here. Live chat (Story 12) uses polling, not WebSockets/SignalR, per the same team decision. Web forms (Story 14) is the one exception — it needs no external provider at all, since it's a direct request from this app's own frontend.
- Story 15's own plan documents a sequencing correction: `TicketMessage.Channel` (originally expected in Story 15 per the initial intake) was actually added in Story 10, since the ingestion path needed it from the start.
