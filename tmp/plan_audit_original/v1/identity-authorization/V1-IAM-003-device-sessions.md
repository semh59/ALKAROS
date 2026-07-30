# V1-IAM-003 - Implement device session lifecycle

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement device-bound session creation, expiry and revocation for cashier and waiter clients.

## Owned surface

- `src/Modules/Identity/DeviceSessions/**`, `tests/Modules/Identity/DeviceSessions/**`, `database/migrations/V1/V1-IAM-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Hashed session tokens, device identity, expiry, revocation and reconnect behavior allowed by V0-ARC-002.

## Out of scope

- Offline order queue and public QR tokens.

## Dependencies

- V1-IAM-001,V0-ARC-002

## Deliverables

- V1-IAM-003 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Revoked/expired sessions cannot submit; raw tokens are never persisted; reconnect preserves only allowed queued operations.

## Handoff

- V1-ORD-002.

