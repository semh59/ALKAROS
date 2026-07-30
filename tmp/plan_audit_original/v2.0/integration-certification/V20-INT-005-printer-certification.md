# V20-INT-005 - Certify printer models

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Certify each approved printer model and transport for routing, encoding, paper failure and retry behavior.

## Owned surface

- `release/evidence/integrations/printers/**`
- Bu görev printing implementation kodunu değiştiremez.

## In scope

- Model/firmware/transport inventory, Turkish characters, cut/feed, disconnect, paper-out, restart, retry and physical duplicate observation.

## Out of scope

- Printer driver implementation, kitchen ticket content rules and fiscal devices.

## Dependencies

- V0-PRN-001, V1-KIT-002, V1-KIT-003, V1-KIT-004

## Deliverables

- Physical-device matrix, sample outputs and retry observation log.

## Acceptance evidence

- Every enabled model produces readable routed output and its failure/retry behavior matches the documented at-least-once limitation and operator recovery procedure.

## Handoff

- V20-UAT-001 and V20-GAT-002.
