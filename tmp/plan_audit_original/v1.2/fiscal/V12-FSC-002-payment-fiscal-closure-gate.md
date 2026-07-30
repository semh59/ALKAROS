# V12-FSC-002 - Implement payment-fiscal closure gate

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Apply the approved legal/device policy to decide when a financially covered Bill may close or must reconcile.

## Owned surface

- `src/Modules/Fiscal/PaymentClosureGate/**`, `tests/Modules/Fiscal/PaymentClosureGate/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Required document strategy, issued/pending/rejected/unknown outcomes and atomic Bill transition.

## Out of scope

- Fiscal document transport implementation details.

## Dependencies

- V12-ALC-002,V12-HUG-001,V12-FSC-001,V0-CMP-001

## Deliverables

- V12-FSC-002 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Bill closure matrix covers cash/card and fiscal outcomes; blocking conditions cannot be bypassed by direct status update.

## Handoff

- V12-REC-001.

