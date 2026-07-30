# V12-PUI-002 - Implement cashier CashSession UI

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.2 scope plus validated provider contract and audit corrections.

## Goal

Implement open, count, close and difference confirmation flow for the active terminal/cashier.

## Owned surface

- `src/Clients/Cashier/Payments/CashSession/**`, `tests/Clients/Cashier/Payments/CashSession/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Opening balance, cash in/out, count, expected/actual difference, permission and stale-version handling.

## Out of scope

- Bank/meal-card payment screens and reconciliation dashboard.

## Dependencies

- V12-CSH-001,V12-CSH-002,V1-CSH-001

## Deliverables

- V12-PUI-002 için production implementation.
- Contract/UI ve otomatik success/failure/retry testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Second open session is prevented; close requires count; difference cannot be overwritten and remains audited.

## Handoff

- V12-PUI-003.

