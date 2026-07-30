# V14-QRS-003 - Implement QR customer session lifecycle

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Create a revocable customer session after QR token validation without turning the raw table token into a reusable browser credential.

## Owned surface

- `src/Modules/QrOrdering/CustomerSession/**`, `tests/Modules/QrOrdering/CustomerSession/**`, `database/migrations/V14/V14-QRS-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Session issuance, hashed persistence, idle/absolute expiry, revocation, table binding and cookie/header security policy.
- Session-to-token lineage and audit events.

## Out of scope

- QR token generation, relay transport, menu rendering and order creation.

## Dependencies

- V14-QRS-001, V14-QRS-002

## Deliverables

- Customer session implementation and versioned contract.
- Expiry, replay, revocation, token-rotation and cross-table isolation tests.
- Forward and rollback migration when persistence is required.

## Acceptance evidence

- A captured raw session cannot access another table; revoked, idle-expired and absolute-expired sessions fail with audited reason codes.

## Handoff

- V14-CWB-001 and V14-CWB-002.
