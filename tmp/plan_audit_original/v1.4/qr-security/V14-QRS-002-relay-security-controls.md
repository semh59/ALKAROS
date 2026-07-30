# V14-QRS-002 - Implement QR relay authentication and abuse controls

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Authenticate relay messages and enforce replay, rate-limit and payload-size controls before local command dispatch.

## Owned surface

- `src/Modules/QrOrdering/RelaySecurity/**`, `tests/Modules/QrOrdering/RelaySecurity/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Signature/key rotation, nonce, timestamp window, per-token/IP limits and safe rejection.

## Out of scope

- QR order business validation and local network deployment.

## Dependencies

- V14-QRS-001,V0-QRG-001,V1-FND-002

## Deliverables

- V14-QRS-002 için production implementation.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Replay and tampered payloads are rejected; rate limit produces no Order; local service exposes no public inbound endpoint.

## Handoff

- V14-QRO-001.

