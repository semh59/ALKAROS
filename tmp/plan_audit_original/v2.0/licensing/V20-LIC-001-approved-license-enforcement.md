# V20-LIC-001 - Implement approved license enforcement

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: implementation

## Goal

Implement only the license enforcement behavior approved by the V0 contract, including an explicit not-applicable path when no enforcement is approved.

## Owned surface

- `src/Modules/Licensing/**`, `tests/Modules/Licensing/**`, `database/migrations/V20/V20-LIC-001/**`
- Bu görev lisans iş kuralını yeniden tanımlayamaz.

## In scope

- Signed license validation, scope/expiry checks, clock-tamper policy, offline grace and auditable enforcement when required by V0-LIC-001.
- If enforcement is not approved: signed not-applicable evidence and proof that no hidden enforcement path exists.

## Out of scope

- Inventing a license server, sales policy, remote kill switch or unapproved telemetry.

## Dependencies

- V0-LIC-001, V15-SEC-001, V1-SET-001

## Deliverables

- Approved enforcement implementation and tests, or approved not-applicable evidence.
- Failure/recovery reason codes and operator-visible diagnostics when applicable.

## Acceptance evidence

- Behavior matches every V0 contract case; network loss, expiry and clock cases cannot silently corrupt orders, payments or fiscal operations.

## Handoff

- V20-LIC-002 and V20-GAT-001.
