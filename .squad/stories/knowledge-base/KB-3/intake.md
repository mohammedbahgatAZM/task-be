# Story intake

- Folder: `.squad/stories/knowledge-base/KB-3/intake.md`

---

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `KB-3`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Solutions and guides
```

---

## Description

```
Role: Support Agent
As a support agent, I want access to step-by-step solution guides, so that I can resolve tickets consistently and correctly.
```

---

## Acceptance criteria

```
- Solution guides are linked to relevant ticket categories for easy discovery.
- Guides support rich formatting (numbered steps, screenshots, videos).
- Guides can be flagged as outdated and routed for review.
- Only authorized editors can publish or modify a guide.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** KB-2 (`Article`'s rich-body/attachment/`KbContentStatus` pattern, reused for `Guide`), Ticket Management TM-2 (`TicketCategory`, linked here for discovery).
- **Depends on code areas or other stories:** backend KB-2, TM-2 (`src/SupportCrm.Domain/Entities/TicketCategory.cs`).

## Extra notes (optional)

- `Guide` mirrors `Article` (KB-2) closely — same rich-text body field, same `KbContentStatus` (`Draft`/`Published`/`Archived`), same attachment pattern (own `GuideAttachment` table + `LocalDiskGuideAttachmentStorage`, following the established one-storage-class-per-owner convention) — deliberately a separate entity/table rather than a shared "content item" base type, consistent with this codebase never having introduced entity inheritance/TPH elsewhere (e.g. `CustomerNote` vs `TicketNote` are separate tables, not a shared base).
- "Screenshots and videos" — screenshots are `GuideAttachment` rows (images), same as KB-2. Videos are **not** file-uploaded/transcoded in this story — store a video URL field (`VideoUrl`, nullable) pointing to an externally-hosted video, since no video storage/streaming infrastructure exists anywhere in this codebase and building one is well outside this story's scope. Flag explicitly.
- "Linked to relevant ticket categories" — a `Guide` has a many-to-many relationship to `TicketCategory` via a join entity (`GuideTicketCategory`, mirroring `TicketAttachment`'s simple two-FK-row pattern), not a single `CategoryId` — a guide can reasonably apply to more than one ticket category.
- "Flagged as outdated and routed for review" — add an `IsFlaggedOutdated` boolean + `FlaggedReason`/`FlaggedAtUtc` on `Guide`, settable by any agent (flagging is a "raise a concern" action, not a privileged one). This is distinct from KB-5's formal `UnderReview` `KbContentStatus` value: flagging *marks* a guide as needing attention without changing its published visibility (a flagged-but-still-`Published` guide keeps serving agents while an editor investigates) — an editor decides separately whether to transition it into `UnderReview`/unpublish, via KB-5's workflow endpoints.
- "Only authorized editors can publish or modify a guide" — add a minimal `Agent.IsKnowledgeBaseEditor` flag (same stand-in pattern as SLA & Automation's `Agent.IsSupervisor`, SA-3), checked at the service layer against a client-supplied editor agent id (same "no real auth, client-supplied actor" convention used throughout this codebase, e.g. Ticket Management's `ChangedBy`). Reading a guide is unrestricted (any agent); creating/editing/publishing requires `IsKnowledgeBaseEditor == true`, enforced by throwing (→ `403`) rather than silently allowing.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Continue the `KnowledgeBase` bounded concern: `src/SupportCrm.Domain/Entities/Guide.cs`, `GuideAttachment.cs`, `GuideTicketCategory.cs`, `src/SupportCrm.Application/KnowledgeBase/GuideService.cs`, `src/SupportCrm.Api/Controllers/GuidesController.cs`.
- `Agent.IsSupervisor`/`SetSupervisor` (SLA & Automation Story 23, `src/SupportCrm.Domain/Entities/Agent.cs`) is the exact precedent for `IsKnowledgeBaseEditor`/`SetKnowledgeBaseEditor`.

## Out of scope

- FAQs (KB-1) and help articles (KB-2, already done) — this story is Guides specifically.
- Video upload/hosting/transcoding — only an external `VideoUrl` field.
- Full-text search (KB-4) and the formal draft/review/publish/scheduled-review workflow (KB-5) — outdated-flagging here is a simple flag, not the full workflow.
