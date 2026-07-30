# V12-FSC-001 - Implement FiscalDocument lifecycle

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Persist sale, cancellation and refund fiscal documents with provider/device references and immutable request history.

## Owned surface

- `src/Modules/Fiscal/DocumentLifecycle/**`, `tests/Modules/Fiscal/DocumentLifecycle/**`, `database/migrations/V12/V12-FSC-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Canonical transitions, document type/strategy, bill/payment/refund source integrity and sanitized payload storage.

## Out of scope

- Payment approval transport and QNB invoice generation.

## Dependencies

- V0-CMP-001,V12-ALC-003,V0-DAT-002

## Deliverables

- V12-FSC-001 için production implementation.
- Başarı, ret, timeout/retry ve finansal invariant testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Exactly one valid source relationship is enforced; rejected/unknown issuance is recoverable without rewriting prior attempts.

## Handoff

- V12-FSC-002 and V12-REC-001.

