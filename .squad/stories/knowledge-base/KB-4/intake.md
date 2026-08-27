# Story intake

- Folder: `.squad/stories/knowledge-base/KB-4/intake.md`

---

## Feature

- **Feature name (display):** Knowledge Base
- **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `KB-4`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Search
```

---

## Description

```
Role: Customer / Agent
As a user, I want to search the knowledge base by keyword, so that I can quickly find relevant content.
```

---

## Acceptance criteria

```
- Search returns results ranked by relevance across FAQs, articles, and guides.
- Search supports Arabic and English queries, including partial/fuzzy matches.
- Search results show a snippet with the matched keyword highlighted.
- Zero-result searches are logged to identify content gaps.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** KB-1 (`Faq`), KB-2 (`Article`), KB-3 (`Guide`) — this story searches across all three.
- **Depends on code areas or other stories:** backend KB-1, KB-2, KB-3.

## Extra notes (optional)

- No search engine (Elasticsearch/etc.) or Postgres full-text-search (`tsvector`/`GIN`) infrastructure exists anywhere in this codebase — the codebase's existing search precedent (`CustomerRepository.SearchAsync`, `src/SupportCrm.Infrastructure/Persistence/CustomerRepository.cs`) is a plain EF Core `.Contains()` predicate. For genuine partial/fuzzy matching (not just substring) without standing up a separate search service, enable PostgreSQL's built-in `pg_trgm` extension (`CREATE EXTENSION IF NOT EXISTS pg_trgm` in the migration) and use trigram `similarity()`/`%` matching via `Npgsql.EntityFrameworkCore.PostgreSQL`'s trigram function mapping — a small, self-contained upgrade over plain `Contains`, not a new service dependency. Flag this as the concrete mechanism so the plan doesn't hand-wave "fuzzy" — if trigram function mapping proves unavailable in the installed Npgsql EF version, fall back to `Contains`-only matching and note the gap explicitly rather than silently downgrading behavior.
- "Ranked by relevance across FAQs, articles, and guides" — compute a simple weighted score per result (e.g. exact/prefix title match > trigram similarity on title > trigram similarity on body), not a general relevance/BM25 engine; combine the three content types' results in application code after querying each independently (three simple queries, not one cross-table UNION query), then sort by score and take the top N.
- "Snippet with the matched keyword highlighted" — server extracts a fixed-length window of body text around the first case-insensitive match of the query and wraps the match in a simple marker (e.g. `**query**` or `<mark>` — pick one consistent convention and document it in the DTO) for the frontend to render; no diff/tokenization library needed.
- Only `Published` `Article`/`Guide` content (KB-2/KB-3's `KbContentStatus`) is searchable — `Draft`/`Archived` never appear in results, mirroring their own read-endpoint visibility rules. FAQs (KB-1) have no status field, so all FAQs are searchable.
- Arabic queries need to match the bilingual `*En`/`*Ar` field pairs KB-1/KB-2/KB-3 established — search both fields per content type and take whichever matched (do not require the caller to specify a language).
- "Zero-result searches are logged" — persist `SearchLog(Query, ResultCount, SearchedAtUtc)` on every search call, not only zero-result ones (simpler to always log and filter by `ResultCount == 0` when reviewing content gaps, than to special-case the zero path).

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- New file: `src/SupportCrm.Application/KnowledgeBase/KbSearchService.cs`, `src/SupportCrm.Domain/Entities/SearchLog.cs`, `src/SupportCrm.Api/Controllers/KbSearchController.cs`.
- `CustomerRepository.SearchAsync` (`src/SupportCrm.Infrastructure/Persistence/CustomerRepository.cs`, lines 15–20) is the existing (non-fuzzy) search precedent in this codebase — read it before deciding how far to extend it with `pg_trgm`.
- Program.cs (`src/SupportCrm.Api/Program.cs`) already configures `UseNpgsql` (see `AddInfrastructure`, `src/SupportCrm.Infrastructure/DependencyInjection.cs`) — `pg_trgm` is a standard bundled Postgres extension, no new package needed, only `Npgsql.EntityFrameworkCore.PostgreSQL`'s existing trigram function support (verify the installed version maps `EF.Functions.TrigramsSimilarity`/`ILike` before relying on it; these are documented Npgsql EF provider features).

## Out of scope

- A dedicated search engine/index (Elasticsearch, Azure Cognitive Search, etc.) — Postgres-native matching only.
- True linguistic stemming/tokenization for Arabic — trigram similarity approximates fuzzy matching without language-aware tokenization; flagged as a known limitation, not solved here.
