# V20-INT-006 - Certify QR public path

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Certify the complete public QR path from scan to pending order under the approved network and security topology.

## Owned surface

- `release/evidence/integrations/qr-public-path/**`
- Bu görev QR uygulama veya relay kodunu değiştiremez.

## In scope

- TLS/domain path, token/session expiry, relay authentication, abuse limits, mobile browsers, accessibility and pending-order creation.

## Out of scope

- Staff confirmation, online delivery channels and customer payment.

## Dependencies

- V14-QRS-001, V14-QRS-002, V14-QRS-003, V14-CWB-001, V14-CWB-002, V14-QRO-001

## Deliverables

- Device/browser/network matrix and redacted security/functional transcripts.

## Acceptance evidence

- Approved mobile/network cases reach exactly one pending order; expired, replayed, cross-table and rate-limited cases are rejected and audited.

## Handoff

- V20-UAT-001 and V20-GAT-002.
