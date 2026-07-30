# V20-INT-002 - Certify QNB e-invoice integration

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Certify the approved QNB environment and document lifecycle using real sandbox responses and reconciliation evidence.

## Owned surface

- `release/evidence/integrations/qnb/**`
- Bu görev QNB adapter kodunu değiştiremez.

## In scope

- Authentication, send, poll, webhook, retry, cancellation, duplicate prevention and provider/internal status reconciliation.

## Out of scope

- Adapter implementation, taxpayer applicability decision and Hugin fiscal flow.

## Dependencies

- V13-QNB-001, V13-QNB-002, V13-QNB-003, V13-QNB-004, V13-QNB-005

## Deliverables

- Sandbox certification matrix, redacted request/response transcripts and reconciliation report.

## Acceptance evidence

- All approved success, rejection, timeout, replay and cancellation scenarios end in one traceable internal/provider outcome with no duplicate document.

## Handoff

- V20-GAT-002.
