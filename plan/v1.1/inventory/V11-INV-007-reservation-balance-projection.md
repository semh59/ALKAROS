# V11-INV-007 - Implement reserved and available stock projection

- Task ID: V11-INV-007
- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation
- Surface state: Planned

## Source basis

- PDF:I.21-I.25
- PDF:II.2.12
- PDF:II.3.9
- PDF:II.5.6
- PDF:II.5.14
- PDF:III.14
- CORR:C27

## Goal

On-hand projection ve authoritative PortionReservation lifecycle'ından reserved ve available balance değerlerini
üretmek ve full rebuild yapmak.

## Owned surface

- `src/Modules/Inventory/ReservationBalanceProjection/**`,
  `tests/Modules/Inventory/ReservationBalanceProjection/**`, `database/migrations/V11/V11-INV-007/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Active reservation aggregation, Released/Consumed/Wasted terminal effects, available formula, atomic projection update,
  drift detection ve full rebuild.

## Out of scope

- Reservation command/arbitration, StockMovement write, cancellation classification ve DailyMenu counters.

## Dependencies

- V11-INV-002
- V11-RSV-001
- V0-DAT-004

## Deliverables

- Reserved/available projection production code'u, migration ve lifecycle/rebuild/concurrency automated tests.

## Acceptance evidence

- Projection silinip on-hand ledger ve reservation history'den aynı reserved/available değerlerine yeniden kurulur.
- Reserved available'ı bir kez azaltır; Released/Consumed/Wasted active reservation'ı bir kez kapatır; replay, crash ve
  concurrent terminal events negatif available veya ikinci etki oluşturmaz.

## Handoff

- V11-RSV-002
- V11-MNU-002
- V11-UI-003
