# V20-MIG-001 - Rehearse production migration

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Execute the complete production migration path on a representative sanitized dataset and measure integrity, duration and resource use.

## Owned surface

- `release/evidence/migration/forward/**`, `tools/release/migration-rehearsal/**`
- Bu görev ürün migration dosyalarını değiştiremez; hata ilgili migration sahibine geri döner.

## In scope

- Preflight checks, backup checkpoint, ordered migration, integrity queries, reconciliation totals and timing.

## Out of scope

- Schema redesign, data cleansing policy and production execution.

## Dependencies

- V0-DAT-001, V15-BKP-002, V15-REC-001, V20-INS-001

## Deliverables

- Reproducible rehearsal procedure and signed result record.
- Before/after row, money, stock and invoice control totals.

## Acceptance evidence

- The release candidate migrates within the approved window and every approved integrity/control-total query passes on the representative dataset.

## Handoff

- V20-MIG-002 and owners of any failed migration.
