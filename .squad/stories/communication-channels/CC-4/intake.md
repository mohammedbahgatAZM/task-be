# Story intake

- Folder: `.squad/stories/communication-channels/CC-4/intake.md`

---

## Feature

- **Feature name (display):** Communication Channels
- **Feature slug (folder under `plans/`):** `communication-channels`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CC-4`
- **Work item type:** `Story`

---

## Title

```
SMS
```

---

## Description

```
Role: Customer
As a customer, I want to send and receive SMS updates about my ticket, so that I stay informed even without internet access.
```

---

## Acceptance criteria

```
- Key ticket status changes can trigger an outbound SMS to the customer.
- A customer can reply to an SMS to add a comment to their existing ticket.
- SMS character limits and multi-part messages are handled without data loss.
- SMS delivery failures are logged and visible to the agent.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CC-1 (shared ingestion path), Ticket Management TM-1/TM-4/TM-5.
- **Depends on code areas or other stories:** backend CC-1's ingestion service, TM-4's `ICustomerStatusNotifier` seam (this story gives it a real, if mocked, implementation for SMS).

## Extra notes (optional)

- **No real SMS gateway exists** — `ISmsSender` is a mock/logging seam, same decision as CC-1/CC-2. Inbound SMS replies arrive via a stub webhook standing in for the gateway's inbound-message callback.
- **This story is the first real implementation of TM-4's `ICustomerStatusNotifier`** (previously `NoOpCustomerStatusNotifier`) — replace that DI registration with an `SmsCustomerStatusNotifier` that calls the mock `ISmsSender` when a ticket's status changes and `NotifyCustomer` was requested.
- **Multi-part messages** — standard SMS is 160 GSM-7 characters (70 for UCS-2/unicode) per segment; model a simple splitter that chunks an outbound message into ordered parts with a part-count/part-index, so "handled without data loss" is a real, testable behavior (concatenation on the mock receiving end, not just a length check).
- **Delivery failures** — model a delivery-status entry per outbound SMS (Sent, Failed), settable via a stub webhook standing in for the gateway's delivery-report callback, surfaced on the ticket.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on CC-1's ingestion service and TM-4's `ICustomerStatusNotifier`.

## Out of scope

- Real SMS gateway integration (Twilio, Vonage, etc.).
