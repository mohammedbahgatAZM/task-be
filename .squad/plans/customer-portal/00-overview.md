# customer-portal — plan overview

Entry point for the **customer-portal** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 35 | [35-story-CP-1.md](35-story-CP-1.md) | Submit tickets | CP-1 | Ticket Management TM-1/TM-2, AI Features Story 32 |
| 36 | [36-story-CP-2.md](36-story-CP-2.md) | Track requests | CP-2 | Story 35 |
| 37 | [37-story-CP-3.md](37-story-CP-3.md) | View history | CP-3 | Story 36 |
| 38 | [38-story-CP-4.md](38-story-CP-4.md) | Access FAQs | CP-4 | Story 35, Knowledge Base Story 28 |
| 39 | [39-story-CP-5.md](39-story-CP-5.md) | Submit feedback | CP-5 | SLA & Automation Story 23, Agent Dashboard Story 18 |

## Dependency notes

- Story 35 introduces this app's first customer-facing identity concept — a `Customer.CustomerNumber` lookup, the same "no real auth, client tracks an id" pattern already used for `AgentContextService` (Agent Dashboard AD-1), plus a new `TicketChannel.Portal` value and two optional `CreateTicketRequest` fields (`CustomerId`, `CategoryId`) that extend Ticket Management TM-1's `TicketService.CreateAsync` without changing its behavior for any other caller.
- Stories 36–39 all lean heavily on reuse: 36's "reply" is a new inbound-message endpoint (not the outbound `ChannelReplyDispatcher`), 37's reopen reuses TM-4's `RecordStatusChangeAsync` verbatim, 38 adds almost nothing beyond two analytics endpoints (search/FAQ display already existed via Knowledge Base KB-1/KB-4), and 39 reuses Agent Dashboard AD-3's `TicketTask` entity directly rather than inventing a parallel one.
- `CustomerPortalOptions` (new, Story 37) ends up holding both the reopen window (Story 37) and the low-rating threshold (Story 39) — one shared options class for the feature, same convention as `AiFeaturesOptions`.
