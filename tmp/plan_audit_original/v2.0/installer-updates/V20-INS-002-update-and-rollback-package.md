# V20-INS-002 - Build and verify update rollback package

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Update an approved prior installation to the release candidate and recover safely when the update fails before or after migration.

## Owned surface

- `updater/**`, `tools/release/update/**`, `tests/Installer/UpdateRollback/**`
- Bu görev uygulama modüllerinin iş mantığını veya migration içeriğini değiştiremez.

## In scope

- Artifact verification, compatibility preflight, maintenance boundary, update sequencing, failure checkpoints and application-binary rollback.

## Out of scope

- Data rollback implementation, automatic production rollout and silent forced updates.

## Dependencies

- V20-INS-001, V0-DAT-001, V15-BKP-002

## Deliverables

- Signed updater and rollback artifact.
- Clean success/failure checkpoint matrix and automated tests.

## Acceptance evidence

- Each injected failure leaves either the prior healthy version or the new healthy version, with an explicit data-recovery instruction and no mixed binary state.

## Handoff

- V20-MIG-001 and V20-REL-001.
