# Story intake

- Folder: `.squad/stories/platform/PL-5/intake.md`

## Feature

- **Feature name (display):** Platform
- **Feature slug (folder under `plans/`):** `platform`

## Tracker

- **Tracker type:** `none` · **Work item id:** `PL-5` · **Work item type:** `Story`

## Title

```
Custom branding
```

## Description

```
Role: System Administrator
As a system administrator, I want to apply custom branding to the portal and communications, so that they match our company identity.
```

## Acceptance criteria

```
- An admin can upload a logo and set brand colors for the customer portal and email templates.
- Branding changes apply consistently across portal, emails, and chat widget.
- A preview is available before branding changes are published live.
- Multi-branch or multi-brand setups can apply different branding per branch, if configured.
```

## Dependencies

- **Blocked by / related ids:** Story PL-4 (`Branch`) — per-branch branding override.
- **Depends on code areas or other stories:** existing local-disk attachment-storage pattern (Ticket/Article/Guide attachments) — `IBrandingAssetStorage` mirrors it exactly for the logo file.

## Extra notes

- `BrandingSettings` (one row per scope: `BranchId = null` is the global default, a non-null `BranchId` overrides it for that branch) — `LogoStorageKey`, `PrimaryColorHex`, `SecondaryColorHex`, `UpdatedBy`, `UpdatedAtUtc`.
- **Real preview, not cosmetic**: `POST .../preview` validates the proposed colors/logo key and echoes back exactly what publishing would produce — **no persistence** — mirroring Security & Administration SEC-4's `validate`/`apply` split exactly (same pattern, same discipline). `POST .../publish` persists.
- `GetEffectiveAsync(branchId)` resolution order: that branch's own override → the global (`BranchId = null`) row → this app's own existing hardcoded brand defaults (`#1565c0`/`#5e35b1` — the exact same Sass variables already set in `frontend/src/styles.scss`, reused as the "nothing configured yet" fallback so an unconfigured system still looks like *this* app, not an unbranded blank one).
- **"Applies… to emails" is honestly bounded**: every "email" sender in this codebase (`MockEmailSender`, and `MockSmsSender`/`MockWhatsAppSender`) is a plain-text mock with no HTML template to brand — there is no real rendered email to show branding *in*. This story exposes the branding data via the same API a future real email-template renderer would consume, but does **not** fabricate an HTML email preview that doesn't correspond to any actual send path. Branding is applied to the two places that are real, rendered, visible UI: the Customer Portal and the chat widget.

## Technical hints

- `src/SupportCrm.Infrastructure/Storage/LocalDiskArticleAttachmentStorage.cs` — the exact shape `LocalDiskBrandingAssetStorage` copies (save-to-disk, return a storage key; a paired download endpoint streams it back by key).
- `frontend/src/styles.scss`, lines 6–7 — the existing `$primary`/`$secondary` Sass values this story's hardcoded fallback colors are lifted from verbatim.

## Out of scope

- Any change to the mock notification senders themselves (see above).
- A rendered HTML email preview (no real HTML email exists to preview).
