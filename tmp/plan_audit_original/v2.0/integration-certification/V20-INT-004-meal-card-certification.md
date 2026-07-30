# V20-INT-004 - Certify meal-card integrations

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Certify every enabled meal-card provider separately against the approved common payment contract.

## Owned surface

- `release/evidence/integrations/meal-card/**`
- Bu görev meal-card adapter kodunu değiştiremez.

## In scope

- Provider identity, sale/refund/cancel, timeout, retry, duplicate prevention, terminal/reference storage and reconciliation.

## Out of scope

- Enabling an unapproved provider, adapter implementation and general card acquiring.

## Dependencies

- V0-MCD-001, V12-MCD-001, V12-MCD-002, V12-MCD-003

## Deliverables

- Provider-by-provider certification records and redacted transcripts.

## Acceptance evidence

- Each enabled provider passes its mandatory real sandbox/device matrix; failed or ambiguous providers remain disabled by configuration.

## Handoff

- V20-GAT-002.
