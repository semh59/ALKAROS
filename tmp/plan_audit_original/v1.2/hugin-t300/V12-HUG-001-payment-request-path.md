# V12-HUG-001 - Implement Hugin T300 payment request path

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement approved and declined card payment flows against the validated T300 contract.

## Owned surface

- `src/Modules/Payments/Hugin/PaymentRequest/**`, `tests/Modules/Payments/Hugin/PaymentRequest/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Request mapping, correlation, approval/decline normalization, sanitized evidence and payment state mutation.

## Out of scope

- Timeout/unknown recovery and refund/cancel.

## Dependencies

- V12-PAY-001,V12-PAY-002,V0-HUG-001

## Deliverables

- V12-HUG-001 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Contract tests plus real sandbox/device transcript show one approved and one declined request with matching references.

## Handoff

- V12-HUG-002 and V12-FSC-002.

