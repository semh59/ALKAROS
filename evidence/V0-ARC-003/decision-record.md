# V0-ARC-003 Decision Record — approved

- Task: V0-ARC-003
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:I.15, PDF:I.48.6
- Access date: 2026-08-02
- Result: Approved
- Artifact: docs/architecture/idempotency-inbox-outbox.md

## Decision summary

Idempotency per client_id+operation_id with SHA-256 request hash and 24h retention; inbox dedup by external event id;
outbox written in same transaction, at-least-once dispatch with exponential backoff; C28/C32 consumers
V1-FND-002/005/006/011.
