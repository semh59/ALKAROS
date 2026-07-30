# V15-BKP-001 - Implement encrypted off-site backup

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Upload verified encrypted database artifacts with retention and key metadata to the validated destination.

## Owned surface

- `src/Modules/Operations/OffsiteBackup/**`, `tests/Modules/Operations/OffsiteBackup/**`, `database/migrations/V15/V15-BKP-001/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Client-side encryption, checksum, retry, retention, immutable artifact metadata and failure alert.

## Out of scope

- Restore orchestration and local backup creation.

## Dependencies

- V1-OPS-002,V0-BKP-001,V15-SEC-001

## Deliverables

- V15-BKP-001 için production implementation veya executable test asset.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Downloaded artifact checksum matches and cannot restore without authorized key; upload failure is visible and retried safely.

## Handoff

- V15-BKP-002.

