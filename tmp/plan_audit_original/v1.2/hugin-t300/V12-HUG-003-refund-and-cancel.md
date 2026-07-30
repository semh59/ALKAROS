# V12-HUG-003 - Implement Hugin refund and cancellation transport

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Send eligible cancellation/refund operations and bind terminal references to the compensating financial record.

## Owned surface

- `src/Modules/Payments/Hugin/RefundTransport/**`, `tests/Modules/Payments/Hugin/RefundTransport/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Eligibility mapping, amount/reference, decline, timeout and idempotent callback handling.

## Out of scope

- Allocation refund calculation and fiscal document creation.

## Dependencies

- V12-HUG-001,V12-HUG-002,V12-ALC-003,V0-HUG-001

## Deliverables

- V12-HUG-003 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Real sandbox/device evidence covers successful refund and timeout query; repeated request produces one terminal operation.

## Handoff

- V12-FSC-001 and V12-REC-001.

