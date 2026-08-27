# Story intake

- Folder: `.squad/stories/customer-management/CM-3/intake.md`

---

## Feature

- **Feature name (display):** Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CM-3`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Interaction history
```

---

## Description

```
Role: Support Agent
As a support agent, I want to see the full history of a customer's past interactions across all channels, so that I have context before responding to them.
```

---

## Acceptance criteria

```
- The customer profile shows a chronological timeline of tickets, calls, chats, and emails.
- Each history entry links directly to the original ticket or conversation.
- The timeline can be filtered by channel, date range, or agent.
- History loads within 2 seconds for a customer with up to 500 past interactions.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CM-1 (Customer profiles). Also consumes notes from CM-4 once that exists.
- **Depends on code areas or other stories:** backend CM-1 (`Customer` aggregate).

## Extra notes (optional)

- Tickets/Calls/Chat/Email source-of-truth modules do not exist anywhere in this codebase yet (only Customer Management stories CM-1..CM-4 exist so far). Design the timeline as an extensible read model / seam (e.g. a `CustomerInteraction` entry type with a `Channel` discriminator) that CM-4 (notes) can feed now, and that future Ticketing/Calls/Chat/Email modules can feed later — do not hard-wire to modules that don't exist. Flag this assumption explicitly in the plan.
- The 2-second load budget for 500 interactions should drive the plan's indexing/pagination approach (e.g. server-side pagination + an index on customer id + timestamp).

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on the `Customer` aggregate from CM-1.

## Out of scope

- Angular/UI implementation (covered by the matching frontend story in the frontend repo).
- Building the Ticketing/Calls/Chat/Email source modules themselves — only the customer-facing timeline aggregation/read model is in scope.
