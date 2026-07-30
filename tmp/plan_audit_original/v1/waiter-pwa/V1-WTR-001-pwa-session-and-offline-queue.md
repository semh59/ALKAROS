# V1-WTR-001 - Implement Waiter PWA session and offline queue

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Implement personal-device session, installable shell and permitted offline operation queue.

## Owned surface

- `src/Clients/WaiterPwa/SessionQueue/**`, `tests/Clients/WaiterPwa/SessionQueue/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Session storage, queued operation ID, reconnect replay, expiry/revocation and unsupported-offline rejection.

## Out of scope

- Order-entry widgets and public QR behavior.

## Dependencies

- V1-IAM-003,V0-ARC-002,V1-FND-002

## Deliverables

- V1-WTR-001 için production implementation.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Browser restart preserves allowed queue; revoked session cannot replay; unsupported finalization never reports success offline.

## Handoff

- V1-WTR-002.

