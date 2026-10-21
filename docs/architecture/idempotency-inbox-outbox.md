# Idempotency, Inbox and Outbox Contract

> **Task:** V0-ARC-003
> **Status:** Done
> **Assignee:** codex-v0-arc-003
> **Work type:** decision
> **Source basis:** PDF:I.15, PDF:I.48.6
> **Date:** 2026-07-30
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

## 1. Idempotency

- Key scope: per-client (client_id + operation_id).
- Request hash: SHA-256 of request body.
- Same key + same hash → return cached response.
- Same key + different hash → reject (IDEMPOTENCY_KEY_CONFLICT).
- Retention: 24 hours.

## 2. Inbox (External → Internal)

- External callbacks (webhooks) stored in inbox table.
- Dedup by external event ID + source.
- Poison event: after 3 failed processing attempts, moved to dead-letter queue.

## 3. Outbox (Internal → External)

- Domain events written to outbox in same transaction as state change.
- Dispatcher reads outbox, sends to external systems, marks as dispatched.
- At-least-once delivery; consumers must be idempotent.
- Dispatch failure: retry with exponential backoff (max 3).

## 4. Affected Tasks

- V1-FND-002

## 5. Implementation status (V1-RMD-666 — code is the source of truth)

The sections above are the approved decision; this section records where the code stands today.

- **Inbox (section 2) is not implemented as a generic mechanism.** The shared `InboxStore`/`IInboxHandler` was deleted by V1-RMD-134
  because nothing used it; each consumer dedupes through its own domain mechanism (for example the provider webhook inbox
  tables). The `inbox_messages` table and its indexes still exist in the schema as dead structure; dropping them is a separate
  migration decision and is deliberately not done here. The "3 failed attempts → dead letter" rule of section 2 has no generic
  implementation.
- **Idempotency conflict error codes (section 1).** One concept, several codes by area, all HTTP 409:
  `IDEMPOTENCY_CONFLICT` (generic API catalog, `ApiErrorCatalog.cs`), `IDEMPOTENCY_KEY_REUSED` (order submission, billing split,
  NFC ordering) and `TENDER_IDEMPOTENCY_KEY_REUSED` (cash and card tenders). `IDEMPOTENCY_KEY_CONFLICT` appears only in a code
  comment. Clients must treat any 409 whose code contains `IDEMPOTENCY` as "same key, different request".
- **Retry budget (section 3).** The default is 3 attempts with exponential backoff (5 s base). Event types delivered to an outside
  party register their own `OutboxRetryProfile` (more attempts, longer waits, capped); Yemeksepeti status sync is one. A message
  whose lease expires counts as an attempt, and so does a delivery that times out (30 s per delivery).
- **Ordering.** Messages are claimed oldest first (`created_at`), but delivery order is **not guaranteed**: a retried message
  is delivered after newer ones, and several dispatcher loops may run. Consumers must tolerate reordering and duplicates.
- **Retention.** Delivered rows are deleted after 14 days and dead-lettered rows after 90 days (hourly purge). Payloads are
  plain JSON, so free text such as a QR order note lives in the table until then.
