# Story intake

- Folder: `.squad/stories/communication-channels/CC-3/intake.md`

---

## Feature

- **Feature name (display):** Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CC-3`
- **Work item type:** `Story`

---

## Title

```
Live chat
```

---

## Description

```
Role: Customer
As a customer, I want to start a live chat on the website, so that I get immediate real-time assistance.
```

---

## Acceptance criteria

```
- A chat widget is available on the website and starts a conversation with one click.
- Chats are routed to an available agent based on queue and skill rules.
- The customer sees a typing indicator and estimated wait time while queued.
- A completed chat is automatically saved as a ticket with full transcript.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CC-1 (shared ingestion path), Ticket Management TM-1/TM-3/TM-5.
- **Depends on code areas or other stories:** backend CC-1's ingestion service (for converting a completed chat into a ticket), TM-3's `Agent` entity.

## Extra notes (optional)

- **Real-time via short-interval polling**, per team decision — no WebSocket/SignalR infrastructure exists in this codebase, and adding it is out of scope for this story. The frontend polls for new messages, typing state, and queue position every few seconds; this is a deliberate simplification, not an oversight.
- **"The website"** — there's no separate public marketing site in this codebase, only the Angular CRM app. The chat widget is modeled as a small, embeddable-feeling standalone route/component within the existing frontend rather than a truly separate embed script — flag this as a scope interpretation.
- **Skill-based routing** is a real capability requiring a skills taxonomy and per-agent skill assignment; given no such data exists, this story implements **queue-based FIFO routing to any available agent** (`Agent.IsAvailable` flag, added to TM-3's `Agent` entity) and documents skill-matching as a future enhancement, not a broken promise — flag explicitly.
- **Estimated wait time** — a naive calculation (queue position × a configured average-handling-time constant), not a statistically modeled estimate.
- **Transcript → ticket** — on chat completion, convert every `ChatMessage` into ticket messages via CC-1's shared ingestion path (`Channel: Chat`), then close the chat session; the ticket's message history is the transcript, not a separate stored document.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on CC-1's ingestion service, TM-3's `Agent` entity (add `IsAvailable`).

## Out of scope

- WebSocket/SignalR-based real-time delivery.
- Skill-based routing (skills taxonomy, per-agent skills, matching algorithm) — FIFO-to-any-available-agent only.
- A true embeddable widget (script tag + iframe) for external websites.
