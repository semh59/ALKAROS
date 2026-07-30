# V20-LIC-002 - Exercise license recovery

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Prove the approved operational recovery path for license expiry, validation failure and authorized renewal without data loss.

## Owned surface

- `release/evidence/licensing/**`
- Bu görev licensing implementation kodunu değiştiremez.

## In scope

- Expiry warning, offline grace exhaustion, invalid signature, clock anomaly, authorized renewal and recovery audit.
- If licensing is not applicable: verification of the approved not-applicable disposition.

## Out of scope

- Changing license policy, issuing commercial licenses and production intervention.

## Dependencies

- V20-LIC-001, V15-RUN-001

## Deliverables

- Scenario transcript and operator recovery evidence.

## Acceptance evidence

- Every applicable failure reaches the documented safe state and authorized recovery restores service without altering financial, stock or audit history.

## Handoff

- V20-GAT-002.
