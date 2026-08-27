# Story intake

- Folder: `.squad/stories/knowledge-base/KB-2/intake.md`

---

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `KB-2`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Help articles
```

---

## Description

```
Role: Support Agent
As a support agent or customer, I want to search help articles, so that I can quickly find or reference a relevant solution.
```

---

## Acceptance criteria

```
- Articles can include text, images, and step-by-step instructions.
- Articles are versioned, showing last-updated date and author.
- An agent can insert a link to an article directly into a ticket reply.
- Article view counts and usefulness ratings are tracked.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** KB-1 (`KbCategory` taxonomy, bilingual-field pattern, helpful/not-helpful voting pattern — all reused here).
- **Depends on code areas or other stories:** backend KB-1; Communication Channels `TicketMessageService`/`ChannelReplyDispatcher` (`src/SupportCrm.Application/Tickets/TicketMessageService.cs`, `ChannelReplyDispatcher.cs`) for the "insert a link into a ticket reply" AC.

## Extra notes (optional)

- "Text, images, and step-by-step instructions" — store the article body as a single rich-text/HTML (or Markdown) string field; do not build a structured step-by-step editor/schema — "step-by-step" is a content-authoring concern (numbered lists in the body), not a distinct data model. Images are separate `ArticleAttachment` rows (own `Id`, own storage key) referenced from the body by URL, mirroring `TicketAttachment`'s shape and `LocalDiskTicketAttachmentStorage`'s per-owner-folder storage pattern (`src/SupportCrm.Infrastructure/Storage/LocalDiskTicketAttachmentStorage.cs`) — a third `LocalDiskArticleAttachmentStorage`, not a shared/generic one, consistent with this codebase's one-storage-class-per-owner-entity convention (Customer vs Ticket already separate).
- "Versioned, showing last-updated date and author" — track `LastUpdatedAtUtc`/`LastUpdatedByName` directly on `Article` (cheap, always-current fields) for this story. Full point-in-time version *snapshots* with rollback are KB-5's job ("Changes to a published article are tracked with version history") — this story's own versioning need is satisfied by the two fields above; do not build a snapshot table here, to avoid duplicating KB-5's work.
- Introduce a shared `KbContentStatus` enum (`Draft`, `Published`, `Archived`) on `Article` from the start, mirroring how Ticket Management's TM-1 defined the full `TicketStatus` vocabulary early and TM-4 later exposed richer public transition actions on top of it. Only `Published` articles are publicly readable/searchable; `Draft`/`Archived` are visible via agent-only endpoints. KB-5 later adds a formal `UnderReview` status, scheduled-review dates, and authorized transition endpoints on top of this same enum — this story just needs the tri-state to exist and to gate visibility.
- "An agent can insert a link... into a ticket reply" — this story only needs to expose a stable article URL/route (e.g. `/kb/articles/{id}`) the frontend can paste into the existing reply composer; no new backend endpoint is required beyond the article's own `GET`, since Communication Channels' reply endpoints (`POST /api/tickets/{id}/reply`, etc.) already accept a free-text body.
- "View counts... tracked" — increment `ViewCount` on `GET /api/kb/articles/{id}`, not on the list endpoint (avoids inflating counts just from browsing a category list).
- "Usefulness ratings" — reuse KB-1's helpful/not-helpful counter pattern (same known dedup gap, same reasoning) rather than inventing a second rating shape.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Continue the `KnowledgeBase` bounded concern from KB-1: `src/SupportCrm.Domain/Entities/Article.cs`, `ArticleAttachment.cs`, `KbContentStatus.cs`, `src/SupportCrm.Application/KnowledgeBase/ArticleService.cs`, `src/SupportCrm.Api/Controllers/ArticlesController.cs`.
- `TicketAttachment.cs` + `LocalDiskTicketAttachmentStorage.cs` + `TicketAttachmentService.cs` (all in `src/SupportCrm.Application/Tickets/` and `src/SupportCrm.Infrastructure/Storage/` respectively) are the exact precedent to mirror for `ArticleAttachment`'s upload/download flow.

## Out of scope

- FAQs (KB-1, already done) and solution guides (KB-3) — this story is Articles specifically.
- Full-text/relevance search (KB-4) — this story only exposes simple `GET`/list endpoints; KB-4 is where searching across FAQs/Articles/Guides together is built.
- Full version-snapshot history and the formal draft/review/publish workflow — both are KB-5.
