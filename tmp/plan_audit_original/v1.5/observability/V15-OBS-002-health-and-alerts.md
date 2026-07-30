# V15-OBS-002 - Implement health checks and alert lifecycle

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Evaluate database, disk, printer, backup and integration health into deduplicated alerts.

## Owned surface

- `src/Modules/Observability/HealthAlerts/**`, `tests/Modules/Observability/HealthAlerts/**`, `database/migrations/V15/V15-OBS-002/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Canonical health/status values, alert rules, acknowledgement/escalation/suppression/resolution and deduplication.

## Out of scope

- External notification channels and reconciliation resolution.

## Dependencies

- V1-OPS-002,V15-REC-001,V0-DAT-002,V0-DOM-001

## Deliverables

- V15-OBS-002 için production implementation veya executable test asset.
- Başarı, ret/failure ve recovery testleri.
- Veri değişiyorsa yalnızca bu task'a ait ileri/geri migration.

## Acceptance evidence

- Repeated same failure yields one active alert; recovery resolves it; stale/failed check cannot report healthy.

## Handoff

- V15-RUN-001 and V20-GAT-002.

