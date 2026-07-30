# V14-QRS-001 - Implement QR token lifecycle

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Issue hashed, revocable and time/policy-bounded table tokens without storing reusable raw secrets.

## Owned surface

- `src/Modules/QrOrdering/TokenLifecycle/**`, `tests/Modules/QrOrdering/TokenLifecycle/**`, `database/migrations/V14/V14-QRS-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Token hash, issuance, rotation, expiry, revocation and table binding.

## Out of scope

- Public relay transport, customer ordering UI and table status.

## Dependencies

- V1.4 entry gate,V0-QRG-001,V0-CMP-003

## Deliverables

- V14-QRS-001 için production implementation.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Database leak exposes no usable raw token; expired/revoked token fails; rotation invalidates prior token as configured.

## Handoff

- V14-QRS-002 and V14-QRO-001.

