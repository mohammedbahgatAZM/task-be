# Story intake

- Folder: `.squad/stories/communication-channels/CC-6/intake.md`

---

## Feature

- **Feature name (display):** Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CC-6`
- **Work item type:** `Story`

---

## Title

```
Unified conversation view
```

---

## Description

```
Role: Support Agent
As a support agent, I want messages from all channels for one customer unified into a single ticket thread, so that I don't have to check multiple systems to get the full picture.
```

---

## Acceptance criteria

```
- Messages from email, WhatsApp, chat, SMS, and web form on the same case appear in one thread.
- The agent can reply from the thread and the response is sent via the original channel.
- Each message in the thread is labeled with its source channel.
- Switching channels mid-conversation does not create a duplicate ticket.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CC-1..CC-5 (all channels), Ticket Management TM-5 (timeline).
- **Depends on code areas or other stories:** backend CC-1's shared ingestion service (dedup already implemented there — this story verifies/exercises it, does not reimplement it), TM-5's `TicketMessage`/`TicketTimelineService`.

## Extra notes (optional)

- **Most of this story's AC is already satisfied by earlier stories, by design** — CC-1 built the shared ingestion path specifically so every channel dedups to the same open ticket (closing AC4 upfront), and `TicketMessage` needs a `Channel` field (added here, since it didn't exist before this story) so the existing TM-5 timeline can label each entry by source channel (AC3) without new tables.
- **"Reply from the thread, sent via the original channel"** — the compose UI on the ticket timeline needs to pick the right outbound sender (`IEmailSender`/`IWhatsAppSender`/`ISmsSender`, or a plain in-app note for Chat/WebForm which have no "reply channel" in the same sense) based on the specific message being replied to (or the ticket's most recent inbound channel) rather than always defaulting to a generic "Agent" message. This is primarily a routing/dispatch concern in the application layer — a `ChannelReplyDispatcher` that picks the right seam by channel.
- This story's real, new work is: adding `Channel` to `TicketMessage`, building the reply-dispatch routing, and end-to-end verification across all 5 channels — not re-deriving the dedup logic.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on CC-1..CC-5 and TM-5.

## Out of scope

- Anything not already covered by CC-1..CC-5's own stories.
