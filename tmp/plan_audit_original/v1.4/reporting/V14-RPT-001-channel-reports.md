# V14-RPT-001 - Implement channel reports

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Report QR and online-channel volume, value, cancellation and reconciliation metrics from approved metric definitions.

## Owned surface

- `src/Modules/Reporting/Channels/**`, `tests/Modules/Reporting/Channels/**`
- Bu görev, başka bir task'ın owned surface alanını değiştiremez.

## In scope

- Source/channel dimensions, business-date filters, order value, rejection/cancellation counts and reconciliation variance.

## Out of scope

- Metric definition changes, operational command handling and consolidated cross-domain dashboard.

## Dependencies

- V0-DOM-008, V14-REC-001, V14-ONL-005

## Deliverables

- Versioned channel report queries/API.
- Golden-dataset tests covering cancellations, retries, time zones and duplicate webhooks.

## Acceptance evidence

- Report totals reconcile to the approved order and reconciliation source records for the same business-date interval.

## Handoff

- V15-RPT-001.
