# V12-TBL-001 - Implement payment-aware table topology

- Task ID: V12-TBL-001
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: integration
- Surface state: Planned

## Source basis

- PDF:I.49
- PDF:II.2.3
- PDF:II.3.16
- PDF:II.5.15
- PDF:III.5

## Goal

Payment durumu bulunan Bill için table transfer, merge ve bill mutation işlemlerini fail-closed policy ile yönetmek.

## Owned surface

- `src/Modules/TableManagement/PaymentTopology/**`, `tests/Modules/TableManagement/PaymentTopology/**`
- Bu görev temel table, Bill, Payment veya allocation schema'sını değiştiremez.

## In scope

- Pending/Unknown lock, partially-paid transfer/merge policy, optimistic concurrency ve auditable typed rejection.

## Out of scope

- Unpaid table topology, payment provider transport, allocation hesaplama ve UI.

## Dependencies

- V1-TBL-002
- V1-TBL-003
- V12-PAY-004
- V12-ALC-002
- V1-FND-005

## Deliverables

- `src/Modules/TableManagement/PaymentTopology/**` altında integration production code'u.
- Active payment, Unknown, partially paid, concurrent mutation ve stale version testleri.

## Acceptance evidence

- Pending veya Unknown payment sırasında transfer, merge ve bill mutation hiçbir ilişkiyi değiştirmeden reddedilir.
- Partially-paid işlem yalnız onaylı policy ile atomik sonuç üretir; allocation yanlış Bill'e taşınmaz.

## Handoff

- V20-UAT-001
- V20-UAT-002
