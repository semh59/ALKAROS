# V15-BKP-002 - Implement isolated restore verification

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Automate restore into an isolated PostgreSQL instance and run integrity/application smoke checks.

## Owned surface

- `src/Modules/Operations/RestoreVerification/**`, `tests/Modules/Operations/RestoreVerification/**`, `deployment/restore/**`, `database/migrations/V15/V15-BKP-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Artifact selection, decrypt, restore, integrity queries, app startup smoke and result record.

## Out of scope

- Production disaster decision and full recovery drill.

## Dependencies

- V15-BKP-001,V0-BKP-001

## Deliverables

- V15-BKP-002 için production implementation veya executable test asset.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Scheduled test restores a real artifact and records measured duration; corrupted artifact fails before application startup.

## Handoff

- V20-DRL-001.

