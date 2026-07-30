# V14-ONL-002 - Implement OnlineExternalOrder normalization

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Create or update one provider record and one internal Order from accepted webhook data.

## Owned surface

- `src/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/**`, `tests/Modules/OnlineOrdering/Yemeksepeti/OrderNormalization/**`, `database/migrations/V14/V14-ONL-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- External identity, normalized customer/order fields, internal link and idempotent version updates.

## Out of scope

- Provider status mapping, cancellation transport and product configuration.

## Dependencies

- V14-ONL-001,V1-ORD-001,V14-MAP-001

## Deliverables

- V14-ONL-002 için production implementation.
- Başarı, ret, replay/race ve güvenlik testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Same external order ID maps to one internal Order; unsupported payload creates explicit rejection/reconciliation, not partial Order.

## Handoff

- V14-ONL-003 and V14-STK-001.

