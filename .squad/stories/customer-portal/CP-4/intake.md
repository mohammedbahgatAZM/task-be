# Story intake

- Folder: `.squad/stories/customer-portal/CP-4/intake.md`

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CP-4`
- **Work item type:** `Story`

---

## Title

```
Access FAQs
```

---

## Description

```
Role: Customer
As a customer, I want to access FAQs from the portal, so that I can find answers on my own before submitting a ticket.
```

---

## Acceptance criteria

```
- FAQs are visible from the portal home page and searchable.
- Relevant FAQs are suggested while the customer is drafting a new ticket.
- FAQ content is available in both Arabic and English.
- Portal analytics show which FAQs reduce ticket creation.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Story CP-1 (the ticket-draft flow FAQs are suggested into).
- **Depends on code areas or other stories:** backend Knowledge Base KB-1 (`Faq`, bilingual by construction already), KB-4 (`KbSearchService.SearchAsync`, reused directly for both "searchable" and "suggested while drafting").

## Extra notes (optional)

- **"Visible from the portal home page and searchable" and "available in Arabic and English"** need zero new backend work — Knowledge Base KB-1's `GET /api/kb/faqs` and KB-4's `GET /api/kb/search` already do exactly this; KB-1's FAQs were bilingual (`*En`/`*Ar` fields) from their very first story.
- **"Relevant FAQs suggested while drafting"** also needs no new *search* logic — the frontend calls KB-4's existing search with whatever the customer has typed so far and filters the results to `contentType === 'Faq'` client-side (same reuse pattern AI Features AI-4 already established for Article/Guide filtering, just inverted). No backend change for this half either.
- **"Portal analytics show which FAQs reduce ticket creation"** is the one genuinely new piece: a lightweight, honest proxy metric — a *deflection rate*, not a causal claim. Model: `FaqPortalImpression(Id, FaqId, DraftSessionId, ShownAtUtc, LedToTicketSubmission bool)`. The frontend generates a random `draftSessionId` (a client-side GUID string) once per ticket-draft attempt; every FAQ shown as a suggestion during that draft logs an impression tied to it (`POST /api/kb/faqs/{id}/impression`). If the customer *does* go on to submit a ticket in that same draft session, the frontend calls a second endpoint (`POST /api/kb/faqs/deflection/mark-converted`) with that `draftSessionId`, flipping every impression sharing it to `LedToTicketSubmission: true`. The deflection rate for an FAQ = the share of its impressions where that flag stayed `false` — "shown, and the customer did *not* go on to submit a ticket in that session" is the closest honest proxy for "this FAQ answered their question" achievable without real session/analytics infrastructure. Flag this proxy nature explicitly in the report, same honesty standard as AI Features AI-3's accuracy report.
- Kept deliberately decoupled from `TicketService.CreateAsync` (no new constructor dependency there) — the frontend makes two sequential calls (create ticket, then mark-converted) rather than threading a `draftSessionId` through ticket creation itself.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- New: `src/SupportCrm.Domain/Entities/FaqPortalImpression.cs`, `src/SupportCrm.Application/CustomerPortal/FaqPortalAnalyticsService.cs`. Endpoints added to the existing `FaqsController` (Knowledge Base KB-1) since they're actions on FAQs, not a new resource.

## Out of scope

- Submitting tickets (CP-1), tracking (CP-2), history (CP-3) — done. Feedback (CP-5) is its own story.
- Any causal/statistically-rigorous deflection measurement — an honest, simple session-based proxy only.
