# V1-BIL-003 - Implement approved discount and fee bill lines

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1 scope plus referenced V0 correction task; undocumented behavior is out of scope.

## Goal

Calculate and persist only approved discount/fee/tip line types under tax and authorization rules.

## Owned surface

- `src/Modules/Billing/Adjustments/**`, `tests/Modules/Billing/Adjustments/**`, `database/migrations/V1/V1-BIL-003/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Line ownership, reason, approval, tax allocation and deterministic rounding.

## Out of scope

- Campaign engine, payroll tip payout and provider promotions.

## Dependencies

- V1-BIL-001,V0-DOM-006,V0-CMP-002,V0-CMP-004

## Deliverables

- V1-BIL-003 için production implementation.
- Public API/event contract varsa contract testleri dahil otomatik testler.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Unsupported fee types are impossible; allowed adjustment totals reconcile line-to-bill and are fully audited.

## Handoff

- V12-ALC-002.

