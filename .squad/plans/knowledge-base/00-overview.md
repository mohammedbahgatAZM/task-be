# knowledge-base — plan overview

Entry point for the **knowledge-base** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 25 | [25-story-KB-1.md](25-story-KB-1.md) | FAQs | KB-1 | None |
| 26 | [26-story-KB-2.md](26-story-KB-2.md) | Help articles | KB-2 | Story 25 |
| 27 | [27-story-KB-3.md](27-story-KB-3.md) | Solutions and guides | KB-3 | Story 26, Ticket Management Story 06 |
| 28 | [28-story-KB-4.md](28-story-KB-4.md) | Search | KB-4 | Story 25, Story 26, Story 27 |
| 29 | [29-story-KB-5.md](29-story-KB-5.md) | Content authoring | KB-5 | Story 26, Story 27 |

## Dependency notes

- Story 25 is foundational: it introduces `KbCategory` (a taxonomy distinct from Ticket Management's `TicketCategory`) and the bilingual-parallel-field pattern (`*En`/`*Ar`) every later story in this feature reuses, plus the helpful/not-helpful counter pattern Stories 26–27 also reuse.
- Story 26 introduces the shared `KbContentStatus` enum (`Draft`/`Published`/`Archived`) and the `ArticleAttachment`/`LocalDiskArticleAttachmentStorage` pattern; Story 27's `Guide` deliberately mirrors both rather than sharing a base type, consistent with this codebase never using entity inheritance elsewhere.
- Story 27 also extends `Agent` with `IsKnowledgeBaseEditor` (mirroring SLA & Automation Story 23's `IsSupervisor`), which Story 29's authorized workflow transitions reuse.
- Story 28 depends on all three content types existing (Stories 25–27) since it searches across them; only `Published` `Article`/`Guide` rows are searchable, `Faq` rows have no status gate.
- Story 29 formalizes the `Draft → UnderReview → Published → Archived` lifecycle on top of Story 26/27's `KbContentStatus`, adding authorized transitions, scheduled-review dates, and version-snapshot history. It supersedes Story 26's lighter `LastUpdatedAtUtc`/`LastUpdatedByName`-only tracking with real point-in-time snapshots. FAQs (Story 25) are explicitly excluded from this workflow.
