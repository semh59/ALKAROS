# V1-OPS-002 - Implement local backup and health foundation

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Schedule local database backup, persist metadata and expose database/disk/backup health states.

## Owned surface

- `src/Modules/Operations/BackupHealth/**`, `tests/Modules/Operations/BackupHealth/**`, `database/migrations/V1/V1-OPS-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Local backup job, checksum, failure state and bounded health history.

## Out of scope

- Off-site upload, restore automation and notification escalation.

## Dependencies

- V1-FND-001,V0-BKP-001,V0-DAT-002

## Deliverables

- V1-OPS-002 için production implementation.
- Owned surface içinde otomatik başarı, ret ve edge-case testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- A real test database backup is produced with checksum; induced failure is visible and does not report success.

## Handoff

- V15-BKP-001, V15-BKP-002 and V15-OBS-002.

