# Story intake

- Folder: `.squad/stories/knowledge-base/KB-5/intake.md`

---

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `KB-5`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Content authoring
```

---

## Description

```
Role: Knowledge Base Manager
As a knowledge base manager, I want to create, edit, and publish articles, so that content stays accurate and current.
```

---

## Acceptance criteria

```
- A draft/review/publish workflow controls when content goes live.
- Articles can be scheduled for review after a set period to catch outdated content.
- Changes to a published article are tracked with version history.
- Articles can be archived or unpublished without being permanently deleted.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** KB-2 (`Article`, `KbContentStatus` = `Draft`/`Published`/`Archived`), KB-3 (`Guide`, same `KbContentStatus`), KB-3's `IsKnowledgeBaseEditor` authorization flag.
- **Depends on code areas or other stories:** backend KB-2, KB-3.

## Extra notes (optional)

- This story formalizes the content lifecycle KB-2 only sketched: extend the shared `KbContentStatus` enum with an `UnderReview` value (`Draft` → `UnderReview` → `Published` → `Archived`, plus `Published`/`UnderReview` → back to `Draft` for revisions), and add explicit, authorized transition endpoints (submit-for-review, approve-and-publish, unpublish/archive) instead of a raw status setter — every transition is checked against `Agent.IsKnowledgeBaseEditor` (KB-3), the same way KB-3 gates guide modification.
- Applies to **both** `Article` (KB-2) and `Guide` (KB-3) — they already share the same `KbContentStatus` enum by design (see KB-3's intake note), so one workflow service parameterized by content type (or two thin services sharing one core state-machine helper) covers both rather than duplicating the transition logic twice. FAQs (KB-1) are explicitly excluded — KB-1's own intake already scoped them out of any formal workflow.
- "Scheduled for review after a set period" — add a nullable `ReviewDueAtUtc` on `Article`/`Guide`, settable at publish time (e.g. "review again in 90 days") or manually by an editor; a query (`GetDueForReviewAsync`) surfaces items past their due date for a manager to act on. No automatic background job is added in this story to auto-flag them — unlike SLA & Automation's escalation story, there's no acceptance criterion requiring automatic action here, only that overdue items are *discoverable* on demand. Flag this as a deliberate scope boundary, not an oversight.
- "Changes to a published article are tracked with version history" — on every edit to a `Published` (or previously-`Published`) item, snapshot the prior body/title/status into a new `ContentVersionEntry` row (content type, content id, version number, snapshot fields, changed-by, changed-at) *before* applying the edit — this supersedes KB-2's lighter `LastUpdatedAtUtc`/`LastUpdatedByName`-only tracking with real point-in-time snapshots, queryable per item. Rollback-to-a-prior-version is not required by the AC ("tracked with version history" ≠ "can be restored") — out of scope unless trivial; flag as a natural follow-up.
- "Archived or unpublished without being permanently deleted" — `Archived` is already a `KbContentStatus` value from KB-2; this story just adds the authorized transition endpoint to reach it from any other state, and confirms (in the plan's edge cases) that no delete/removal path exists anywhere in this feature — archiving is the only way content stops being live.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Extend `src/SupportCrm.Domain/Entities/KbContentStatus.cs` (KB-2) with `UnderReview`. New: `ContentVersionEntry.cs`, `src/SupportCrm.Application/KnowledgeBase/ContentWorkflowService.cs`, endpoints added to `ArticlesController`/`GuidesController` (KB-2/KB-3) rather than a new controller, since these are actions *on* an existing article/guide, matching how Ticket Management added `/status`/`/escalate` directly onto `TicketsController` rather than a separate controller.
- `TicketEscalationEntry`/`TicketStatusChangeEntry` (`src/SupportCrm.Domain/Entities/`) are the closest precedent for an insert-only audit/snapshot table like `ContentVersionEntry`.

## Out of scope

- FAQs (KB-1) — no formal workflow applies to them, per KB-1's own scope.
- Automatic background flagging of overdue-for-review content — discoverable on demand only (see note above); no recurring job like SLA & Automation's escalation service.
- Version rollback/restore — history is tracked and queryable, not automatically revertible.
