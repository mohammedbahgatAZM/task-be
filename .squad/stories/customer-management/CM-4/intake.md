# Story intake

- Folder: `.squad/stories/customer-management/CM-4/intake.md`

---

## Feature

- **Feature name (display):** Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CM-4`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Notes and attachments
```

---

## Description

```
Role: Support Agent
As a support agent, I want to add internal notes and attach files to a customer profile, so that important context and documents are preserved for whoever handles the case next.
```

---

## Acceptance criteria

```
- An agent can add a free-text internal note that is not visible to the customer.
- Files up to a configurable size limit can be attached and previewed/downloaded.
- Notes and attachments show the author's name and timestamp.
- Notes can be pinned so key information stays visible at the top of the profile.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CM-1 (Customer profiles).
- **Depends on code areas or other stories:** backend CM-1 (`Customer` aggregate). Feeds CM-3's interaction timeline.

## Extra notes (optional)

- File storage strategy (local disk vs. object storage) is a greenfield decision — no existing convention in this codebase. Pick the simplest option that keeps a clean seam for swapping the storage backend later (e.g. an `IAttachmentStorage` interface), and note the choice + tradeoff in the plan.
- "Configurable size limit" implies an app-setting/config value, not a hardcoded constant.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on the `Customer` aggregate from CM-1.

## Out of scope

- Angular/UI implementation (covered by the matching frontend story in the frontend repo).
- Virus scanning / content moderation of uploaded files.
- Customer-visible notes (only internal notes are in scope).
