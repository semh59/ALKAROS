# V20-MIG-002 - Rehearse migration rollback

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Prove the approved rollback path from the migrated release candidate to its pre-migration recoverable state.

## Owned surface

- `release/evidence/migration/rollback/**`, `tools/release/rollback-rehearsal/**`
- Bu görev ürün migration veya backup uygulama kodunu değiştiremez.

## In scope

- Rollback trigger point, write freeze, reverse migration or restore decision, execution timing and integrity comparison.

## Out of scope

- Defining RPO/RTO, production rollback and fixing failed migrations.

## Dependencies

- V20-MIG-001, V20-DRL-001

## Deliverables

- Reproducible rollback rehearsal and decision record.
- Restored-state integrity and reconciliation report.

## Acceptance evidence

- The rehearsed path returns the system to the approved checkpoint within RTO and inside the approved RPO loss bound, with all control totals explained.

## Handoff

- V20-GAT-002 and V20-REL-003.
