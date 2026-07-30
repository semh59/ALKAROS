# Local-First Synchronization Contract

> **Task:** V0-ARC-002
> **Status:** InProgress
> **Assignee:** codex-v0-arc-002
> **Work type:** decision
> **Source basis:** PDF:I.1.1, PDF:I.4, PDF:I.51
> **Date:** 2026-07-30

## 1. Sync Model
- Client (waiter PWA) operates offline with local queue.
- Operations carry client-generated UUID + timestamp.
- On reconnect, queue replays to server in order.
- Server deduplicates by operation UUID (idempotency).
- Conflicts resolved by server-side version check (optimistic concurrency).

## 2. Rules
1. Client operations are idempotent (same UUID = same result).
2. Stale version (client version < server version) → rejected, client must refresh.
3. Out-of-order operations: server applies in client timestamp order within same session.
4. Reconnect: full queue replay, server processes each operation once (dedup).

## 3. Affected Tasks
- V1-ORD-002, V1-IAM-003