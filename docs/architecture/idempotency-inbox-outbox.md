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
