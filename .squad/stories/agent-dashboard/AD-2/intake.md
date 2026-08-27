# Story intake

- Folder: `.squad/stories/agent-dashboard/AD-2/intake.md`

---

## Feature

- **Feature name (display):** Agent Dashboard
- **Feature slug (folder under `plans/`):** `agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `AD-2`
- **Work item type:** `Story`

---

## Title

```
Customer information
```

---

## Description

```
Role: Support Agent
As a support agent, I want to see relevant customer information alongside a ticket, so that I don't need to search separately for context.
```

---

## Acceptance criteria

```
- Opening a ticket displays a side panel with the customer's profile, contact details, and open/past tickets.
- Key account flags (e.g. VIP, at-risk) are visible at a glance.
- The agent can navigate to the full customer profile in one click.
- Sensitive customer data is masked for agents without the required permission.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Agent Dashboard AD-1 (agent switcher/identity), Customer Management CM-1/CM-2 (`Customer`, `ContactDetail`), Ticket Management TM-1 (ticket↔customer link).
- **Depends on code areas or other stories:** backend CM-1 (`Customer`, `CustomerService`), CM-2 (`ContactDetail`, `ContactDetailService`), TM-1 (`ITicketRepository.GetByCustomerAsync`).

## Extra notes (optional)

- **Account flags:** `Customer` gets two new booleans, `IsVip` and `IsAtRisk` (the AC's own examples), rather than an open-ended tagging system — extend later if more flag types are needed. Settable from the existing customer profile screen.
- **Team decision — permission model:** the app has no roles/permissions system. This story adds a single boolean, `Agent.CanViewSensitiveData` (default `false` for new agents, so masking is actually observable), toggleable from the minimal agent-admin list introduced in AD-1. "Sensitive" = the customer's `Address` and every `ContactDetail.Value` (email/phone/etc.) — `Name`/`Company`/`Branch`/`CustomerNumber` are not masked (an agent still needs to identify who they're talking to). Since there's no auth middleware, the requesting agent's id is passed explicitly as a query parameter (same "acting as" mechanism as AD-1) — the endpoint looks up that agent's `CanViewSensitiveData` flag itself and masks accordingly server-side (never trust a client-side "am I allowed" flag).
- **Side panel contents:** customer profile fields, contact details (masked per the above), open ticket count, and a short list of past tickets (reference number, subject, status) for the same customer — reusing `ITicketRepository.GetByCustomerAsync`, not a new query mechanism.
- **One-click navigation:** a plain link to the existing customer profile route; no new screen needed for this specific AC.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on `Customer`/`CustomerService` (CM-1), `ContactDetail`/`ContactDetailService` (CM-2), `ITicketRepository.GetByCustomerAsync` (TM-1), the agent switcher (AD-1).
- Masked values render as a fixed placeholder (e.g. `"•••• (restricted)"`), not an empty string — the agent should see *that* something is hidden, not think there's no data.

## Out of scope

- A general-purpose roles/permissions system — one flag, one purpose.
- Masking anywhere other than this side panel (e.g. the full customer profile screen, ticket requester contact value) — scoped to this story's AC only.
