# V20-REL-002 - Execute controlled pilot rehearsal

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Exercise the immutable release candidate in a production-equivalent but non-production environment using synthetic or authorized sanitized data.

## Owned surface

- `release/evidence/pilot/**`
- Bu görev release artifactını veya ürün kodunu değiştiremez.

## In scope

- Installation, representative shift workflow, integrations against approved sandboxes/devices, monitoring, failure triggers, rollback decision timing and defect capture.

## Out of scope

- Real customer data, real payment/fiscal issuance, production deployment and defect fixes.

## Dependencies

- V20-REL-001, V20-INT-001, V20-INT-002, V20-INT-003, V20-INT-004, V20-INT-005, V20-INT-006, V20-UAT-003

## Deliverables

- Pilot transcript, operational metrics, defect register and rollback-decision evidence.

## Acceptance evidence

- Approved workflow and reliability thresholds pass on the exact release artifact; no unresolved critical/high defect remains.

## Handoff

- V20-GAT-002 and V20-REL-003.
