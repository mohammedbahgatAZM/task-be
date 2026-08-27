# platform — plan overview

Entry point for the **platform** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 49 | [49-story-PL-1.md](49-story-PL-1.md) | Arabic & English | PL-1 | — |
| 50 | [50-story-PL-2.md](50-story-PL-2.md) | Web and mobile friendly | PL-2 | — (frontend-only) |
| 51 | [51-story-PL-3.md](51-story-PL-3.md) | Multi-department | PL-3 | Reports & Management Story 40 |
| 52 | [52-story-PL-4.md](52-story-PL-4.md) | Multi-branch | PL-4 | — |
| 53 | [53-story-PL-5.md](53-story-PL-5.md) | Custom branding | PL-5 | Story 52 (`Branch`) |

## Dependency notes

- New bounded concern `SupportCrm.Application.Platform` (Stories 51/52/53's services) — `PlatformDtos.cs` shared across it, one file per the established per-feature convention.
- Story 49 (PL-1) is the only story here with *no* new bounded concern of its own — it adds one field to two already-shipped entities (`Agent`, `Customer`) and does all its real work on the frontend.
- Story 50 (PL-2) has no backend surface at all.
- Stories 51 (departments) and 52 (branches) both extend already-shipped entities with additive nullable FKs — no breaking changes to any prior module's data or behavior.
- Story 53 (branding) depends on 52 for its per-branch override scope.
- One consolidated migration (`AddPlatform`) covers every schema change across all five stories — same "one migration per feature, not per story" discipline established after the SLA & Automation migration-batching lesson earlier in this codebase's history.
- **Two explicit, deliberate scope boundaries carried through this whole feature** (see Stories 51/52's own intake notes for the full reasoning): department/branch-scoped data *visibility* enforcement is not built (the data model is; enforcement is a follow-up), and per-branch business hours are not built (the existing calendar stays global).
