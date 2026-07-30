# V20-UAT-002 - Accept finance and inventory workflows

- Status: Planned
- Assignee: Unassigned (exactly one person)
- Work type: validation

## Goal

Obtain named user acceptance for billing, payments, refunds, cash, customer account, invoicing, purchasing, stock and reporting workflows.

## Owned surface

- `release/evidence/uat/finance-inventory/**`
- Bu görev ürün kodunu veya acceptance sonucunu değiştiremez.

## In scope

- Split payment/refund, cash close, account posting/payment, invoice lifecycle, receiving/correction, production consumption, waste and report reconciliation.

## Out of scope

- Service UI, legal approval, defect fixes and production use.

## Dependencies

- V20-REL-001, V15-RPT-001, V20-INT-001, V20-INT-002, V20-INT-004

## Deliverables

- Executed scripts, named participant sign-offs and financial/stock control totals.

## Acceptance evidence

- Every mandatory script passes and control totals reconcile; failed or unexplained variance remains blocking.

## Handoff

- V20-UAT-003 and defect owners.
