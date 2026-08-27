# Story intake

- Folder: `.squad/stories/platform/PL-2/intake.md`

## Feature

- **Feature name (display):** Platform
- **Feature slug (folder under `plans/`):** `platform`

## Tracker

- **Tracker type:** `none` · **Work item id:** `PL-2` · **Work item type:** `Story`

## Title

```
Web and mobile friendly
```

## Description

```
Role: End User
As a user, I want to access the CRM from web and mobile devices, so that I can work from anywhere.
```

## Acceptance criteria

```
- The application is fully usable on desktop browsers and responsive on tablet/mobile screen sizes.
- Core agent functions (view/reply to tickets, notifications) are available on mobile.
- The customer portal is mobile-friendly for submitting and tracking tickets.
- Performance on mobile networks remains acceptable (defined load-time targets).
```

## Dependencies

- None — this story has **no backend surface at all**; it is a frontend responsive-design audit.

## Extra notes

- This is almost entirely a **confirmation + small-fix** story, not a build: every component across all six prior modules already uses Bootstrap's responsive grid (`col-12 col-md-*`), and the app shell already collapses its sidebar to a toggle-controlled overlay under 992px (`app-shell.component.scss`, pre-existing). PL-2's real work is auditing that this holds, not rebuilding it.
- "Performance on mobile networks remains acceptable (defined load-time targets)" is **not independently verifiable in this environment** — there is no real deployment, no Lighthouse CI, and `ng build`/`ng serve` are already unverified here (documented in every prior frontend plan's Done Criteria). This AC is explicitly flagged as unverifiable rather than backed by a fabricated benchmark number.

## Technical hints

- N/A — backend untouched by this story.

## Out of scope

- Any backend change.
- Synthetic performance benchmarking (see above).
