# V12-HUG-004 - Implement T300 terminal totals reconciliation

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.2 scope plus validated provider contract and audit corrections.

## Goal

Compare local approved/refunded card transactions with the terminal's validated totals or transaction query source.

## Owned surface

- `src/Modules/Reconciliation/HuginTotals/**`, `tests/Modules/Reconciliation/HuginTotals/**`, `database/migrations/V12/V12-HUG-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Period/cutoff identity, terminal reference matching, missing/extra transaction and case creation.

## Out of scope

- Bank settlement outside the validated T300 contract.

## Dependencies

- V12-HUG-001,V12-HUG-003,V12-REC-001,V0-HUG-001

## Deliverables

- V12-HUG-004 için production implementation.
- Contract/UI ve otomatik success/failure/retry testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Known test period reconciles to zero difference; injected missing/extra transaction creates one traceable case.

## Handoff

- V15-REC-001.

