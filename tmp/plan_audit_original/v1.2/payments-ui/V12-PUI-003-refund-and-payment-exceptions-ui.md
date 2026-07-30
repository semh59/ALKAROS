# V12-PUI-003 - Implement refund and payment exception UI

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Source basis

- Master PDF V1.2 scope plus validated provider contract and audit corrections.

## Goal

Implement permissioned full/partial refund, Unknown payment follow-up and fiscal/reconciliation status display.

## Owned surface

- `src/Clients/Cashier/Payments/Exceptions/**`, `tests/Clients/Cashier/Payments/Exceptions/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Eligible allocation selection, refund amount, reason, provider progress, uncertain result and case link.

## Out of scope

- Reconciliation resolution actions reserved for V1.5.

## Dependencies

- V12-ALC-003,V12-HUG-002,V12-HUG-003,V12-FSC-001,V12-REC-001,V1-IAM-002

## Deliverables

- V12-PUI-003 için production implementation.
- Contract/UI ve otomatik success/failure/retry testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Partial refund preview matches server; retry produces one operation; unknown result never displays completed without evidence.

## Handoff

- V15-REC-002.

