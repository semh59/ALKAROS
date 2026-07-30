# V20-DRL-001 - Execute disaster restore drill

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Restore the release candidate from the approved off-site backup into an isolated clean environment and measure recovery objectives.

## Owned surface

- `release/evidence/recovery/**`, `tools/release/restore-drill/**`
- Bu görev backup/restore ürün kodunu değiştiremez.

## In scope

- Backup selection, key access, clean restore, service bootstrap, integrity checks, reconciliation and RPO/RTO measurement.

## Out of scope

- Changing recovery targets, production failover and fixing backup defects.

## Dependencies

- V0-BKP-002, V15-BKP-001, V15-BKP-002, V20-INS-001

## Deliverables

- Timestamped drill transcript, integrity report and measured RPO/RTO.

## Acceptance evidence

- Clean-environment restore completes within approved RTO, data loss remains within approved RPO and all required integrity/reconciliation checks pass.

## Handoff

- V20-MIG-002, V20-GAT-002 and owners of recovery defects.
