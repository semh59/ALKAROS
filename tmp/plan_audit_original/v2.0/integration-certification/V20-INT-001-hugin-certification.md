# V20-INT-001 - Certify Hugin integration

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Certify the approved Hugin model/firmware/protocol combination against fiscal sale, retry, total and failure scenarios.

## Owned surface

- `release/evidence/integrations/hugin/**`
- Bu görev Hugin adapter kodunu değiştiremez.

## In scope

- Device identity, firmware/protocol evidence, sale/refund/cancel cases, timeout/retry, terminal-total reconciliation and redacted raw transcripts.

## Out of scope

- Adapter implementation, fiscal legal interpretation and certification of other devices.

## Dependencies

- V12-HUG-001, V12-HUG-002, V12-HUG-003, V12-HUG-004, V12-FSC-003

## Deliverables

- Device test matrix, redacted transcripts and signed certification result.

## Acceptance evidence

- Every approved mandatory scenario passes on the named physical device/firmware; no retry produces an unexplained duplicate fiscal transaction.

## Handoff

- V20-GAT-002.
