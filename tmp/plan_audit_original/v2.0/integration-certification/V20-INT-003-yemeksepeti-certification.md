# V20-INT-003 - Certify Yemeksepeti integration

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Certify the approved Yemeksepeti contract for inbound orders and outbound status, catalog and availability operations.

## Owned surface

- `release/evidence/integrations/yemeksepeti/**`
- Bu görev online-order adapter kodunu değiştiremez.

## In scope

- Signed webhook/replay, order normalization, mapping, accept/reject/cancel, catalog publish, availability publish, retry and rate-limit cases.

## Out of scope

- Adding unapproved channels, provider contract negotiation and stock calculation.

## Dependencies

- V0-YSP-001, V14-ONL-001, V14-ONL-002, V14-ONL-003, V14-ONL-004, V14-ONL-005

## Deliverables

- Real sandbox test matrix, redacted transcripts and divergence report.

## Acceptance evidence

- Mandatory sandbox scenarios pass and internal/provider order, catalog and availability states reconcile without unexplained duplicates or drift.

## Handoff

- V20-GAT-002.
