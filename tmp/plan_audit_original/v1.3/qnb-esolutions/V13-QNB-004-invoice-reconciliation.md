# V13-QNB-004 - Implement QNB invoice reconciliation

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Create reconciliation cases for submission timeout, local/provider status mismatch and incoming retrieval gaps.

## Owned surface

- `src/Modules/Reconciliation/QnbInvoices/**`, `tests/Modules/Reconciliation/QnbInvoices/**`, `database/migrations/V13/V13-QNB-004/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Outgoing query recovery, incoming checkpoint gap, case deduplication and resolution evidence.

## Out of scope

- Provider transport implementation and unified dashboard.

## Dependencies

- V13-QNB-002,V13-QNB-003,V12-REC-001

## Deliverables

- V13-QNB-004 için production implementation.
- Başarı, ret, retry/idempotency ve veri bütünlüğü testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Timeout never resubmits before query; one mismatch yields one open case with both local and provider references.

## Handoff

- V15-REC-001.

