# V12-ALC-004 - Finalize approved refunds

- Task ID: V12-ALC-004
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.26-I.29
- PDF:I.49
- PDF:II.2.6
- PDF:II.2.16
- PDF:II.5.3-II.5.4
- PDF:III.8
- PDF:III.19

## Goal

Yalnız provider Approved refund sonucundan sonra compensating allocation ve fiscal refund handoff'unu finalize etmek.

## Owned surface

- `src/Modules/Payments/Allocations/RefundFinalization/**`,
  `tests/Modules/Payments/Allocations/RefundFinalization/**`, `database/migrations/V12/V12-ALC-004/**`
- Bu görev provider transport veya RefundIntent eligibility kuralını değiştiremez.

## In scope

- Approved finalization, cumulative limit recheck, durable resume, fiscal handoff ve Unknown reconciliation lock.
- Provider-neutral iade finalization: approved meal-card iadeleri de aynı akıştan geçer (transport: V12-MCD-003 adapter
  sözleşmesi).

## Out of scope

- Refund eligibility, Hugin request mapping, inventory return ve reconciliation case persistence.

## Dependencies

- V12-ALC-003
- V12-HUG-003
- V12-FSC-001
- V1-FND-005
- V12-MCD-003

## Deliverables

- `src/Modules/Payments/Allocations/RefundFinalization/**` altında production code ve migration.
- Approved, Rejected, Unknown, duplicate ve her crash penceresi için failure-injection testleri.

## Acceptance evidence

- 100 payment / 20 Approved refund tam olarak 80 net paid bırakır; Rejected veya Unknown allocation yazmaz.
- Crash sonrası resume aynı provider action için tek compensating allocation ve tek fiscal handoff üretir.

## Handoff

- V12-FSC-002
- V12-REC-001
- V12-PUI-003
