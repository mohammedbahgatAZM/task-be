# Story intake

- Folder: `.squad/stories/platform/PL-1/intake.md`

## Feature

- **Feature name (display):** Platform
- **Feature slug (folder under `plans/`):** `platform`

## Tracker

- **Tracker type:** `none` · **Work item id:** `PL-1` · **Work item type:** `Story`

## Title

```
Arabic & English
```

## Description

```
Role: End User
As a user, I want to use the system in Arabic or English, so that I can work in my preferred language.
```

## Acceptance criteria

```
- All core screens, labels, and system messages are available in both Arabic and English.
- The interface correctly supports right-to-left (RTL) layout when Arabic is selected.
- Each user can set their own language preference independent of others.
- Customer-facing content (portal, notifications, chatbot) respects the customer's chosen language.
```

## Dependencies

- Depends on: none new backend-side beyond adding a `PreferredLanguage` field to `Agent` and `Customer`.

## Extra notes

- This story's backend surface is deliberately small: `PreferredLanguage` (string, `"en"`|`"ar"`, default `"en"`) added to `Agent` and `Customer` — the two identity concepts actually used throughout the operational app (Security & Administration's `User` is a separate, admin-only login concept, not used for day-to-day ticket work, so it's untouched here).
- All translation work (dictionaries, RTL layout, the language switcher) is frontend-only — the backend's only job is to store and return each person's chosen language, which existing content already varies by (`QuestionEn`/`QuestionAr` on `Faq`, etc., unchanged).
- **Explicit scope boundary:** "all core screens" is translated for the customer-facing surfaces this AC names directly (portal) plus the shell/home/login — not literally every one of the ~30 agent-facing admin/operational screens built across five prior modules. Flagged clearly in the frontend plan, not silently partial.

## Technical hints

- `src/SupportCrm.Domain/Entities/Agent.cs`/`Customer.cs` — both gain one new nullable-with-default column, additive, no data loss.

## Out of scope

- Translating every agent-facing screen (see above).
- Localizing notification/email content bodies (`MockEmailSender`/`MockSmsSender`/`MockWhatsAppSender` are plain-text mocks with no template system to localize).
