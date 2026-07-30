# V20-UAT-001 - Accept service workflows

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Obtain named user acceptance for cashier, waiter, table, order, kitchen, QR and printing workflows on the release candidate.

## Owned surface

- `release/evidence/uat/service/**`
- Bu görev ürün kodunu veya acceptance sonucunu değiştiremez.

## In scope

- Role-based scripts, success/failure flows, concurrent table/order cases, kitchen routing, QR pending confirmation and printer recovery.

## Out of scope

- Payment settlement, inventory accounting, defect fixes and production use.

## Dependencies

- V20-REL-001, V20-INT-005, V20-INT-006

## Deliverables

- Executed scripts, named participant sign-offs and defect references.

## Acceptance evidence

- Every mandatory script has pass evidence and named acceptance; failed scripts remain blocking rather than being marked accepted.

## Handoff

- V20-UAT-003 and defect owners.
