# Story intake

- Folder: `.squad/stories/knowledge-base/KB-1/intake.md`

---

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `KB-1`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
FAQs
```

---

## Description

```
Role: Customer
As a customer, I want to browse frequently asked questions, so that I can find answers without contacting support.
```

---

## Acceptance criteria

```
- FAQs are organized by category/topic and publicly accessible from the portal.
- Each FAQ entry can be marked helpful/not helpful by the customer.
- FAQs support both Arabic and English content.
- Unhelpful ratings are visible to knowledge base managers for review.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** None — first, foundational story in this feature.
- **Depends on code areas or other stories:** None directly; establishes the `KbCategory` taxonomy and bilingual-content pattern that Help Articles (KB-2) and Solutions and Guides (KB-3) reuse.

## Extra notes (optional)

- No localization/i18n infrastructure exists anywhere in this codebase. Model Arabic/English as parallel fields on the entity itself (`QuestionEn`/`QuestionAr`, `AnswerEn`/`AnswerAr`, both nullable so either language alone is valid) rather than introducing a generic resource/translation system — flag this explicitly as the simplest stand-in that satisfies the AC. `KbCategory` (name only) follows the same parallel-field pattern (`NameEn`/`NameAr`).
- "Organized by category/topic" needs a new `KbCategory` taxonomy — distinct from Ticket Management's `TicketCategory` (TM-2), which exists for ticket routing/reporting, not knowledge content. Do not reuse `TicketCategory` here; Solutions and Guides (KB-3) is the story that explicitly links back to `TicketCategory` for ticket-context discovery.
- "Marked helpful/not helpful by the customer" — no customer identity/session exists to dedupe repeat votes from the same person (same gap as every other "no auth" area of this codebase). Model as simple atomic counters (`HelpfulCount`/`NotHelpfulCount`) incremented per vote, with no duplicate-vote prevention — flag this explicitly as a known gap, not silently ignored.
- "Publicly accessible from the portal" — no authentication exists anywhere in this codebase, so every read endpoint here is inherently public already; nothing extra to build for that half of the AC beyond just not requiring an actor id to read.
- "Unhelpful ratings visible to knowledge base managers for review" — a query/endpoint sorted by `NotHelpfulCount` (or ratio) is sufficient; no separate "flagged for review" workflow state is needed for FAQs specifically (KB-5's draft/review/publish workflow is scoped to Articles/Guides, not FAQs — see KB-5's Out of scope).

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- New bounded concern `KnowledgeBase` — mirror the existing convention: `src/SupportCrm.Domain/Entities/Kb*.cs`/`Faq*.cs`, `src/SupportCrm.Application/KnowledgeBase/`, `src/SupportCrm.Infrastructure/Persistence/Faq*.cs`, `src/SupportCrm.Api/Controllers/FaqsController.cs`.
- `TicketCategory.cs` (`src/SupportCrm.Domain/Entities/TicketCategory.cs`) is the precedent for `KbCategory`'s simple `(Id, Name, IsActive)` shape — adapted with `NameEn`/`NameAr` instead of a single `Name`.

## Out of scope

- Help articles (KB-2), solution guides (KB-3), full-text search across content types (KB-4), and the draft/review/publish authoring workflow (KB-5) — each is its own story below; this story is FAQ browsing + category taxonomy + helpful/not-helpful voting only.
- Duplicate-vote prevention — no identity system exists to key it on.
