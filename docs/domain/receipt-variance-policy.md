# Receipt Variance Policy — approved decision record

> **Task:** V0-DOM-009
> **Status:** Done
> **Work type:** decision
> **Source basis:** PDF:III.15, CORR:C11
> **Access date:** 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

PDF `III.15` defines only the purchasing schema (`suppliers`,
`purchase_orders`, `goods_receipts`, `goods_receipt_items`); it defines no
policy for quantities received against a purchase order. `CORR:C11` records
that gap. The single binding policy below is approved by the named business
approver.

## Selected decisions

| Case | Selected result | Authority / effect |
| --- | --- | --- |
| Short delivery (received < ordered) | Always recorded as received quantity with the actual quantity; no approval needed | Stock posts the received quantity; the difference is an open purchase-order expectation, not stock |
| Within-tolerance over-receipt (received > ordered, excess ≤ %5 of ordered quantity) | Auto-accept: goods receipt posts the full received quantity | No approval needed; variance is recorded with a mandatory reason |
| Above-tolerance over-receipt (excess > %5) | Requires `Manager` approval to post; without approval the excess is not posted and is rejected | Rejected excess is recorded as a separate rejected line; supplier credit expectation is created for the rejected quantity |
| Rejected quantity | Separate goods receipt line with `rejected` marker; no stock posting | Supplier credit expectation per rejected line; reconciliation at settlement |
| Tolerance basis | `%5` computed on the ordered quantity of the same `goods_receipt_item` line | Single tolerance for both over and under delivery; under-delivery has no approval |
| Reason | Every variance (short, over, rejected) carries a mandatory reason | Audit-first (PDF III.1.7) |
| Audit timing | Variance decisions are recorded in the goods receipt at `received_at`; later supplier reconciliation does not rewrite receipt lines | Receipt lines are historical records (PDF III.37 immutable history) |

## Rejected alternatives

- Silent auto-accept of any excess — rejected: "onaysız over-receipt davranışı
  yoktur" (acceptance) and excess stock misstates inventory.
- Tolerance per supplier — rejected: single `%5` rule is simpler and the
  business has one purchasing policy.
- Posting rejected quantity and crediting later — rejected: stock would
  temporarily hold quantity the supplier will not invoice.
- Percentage of received quantity — rejected: tolerance must anchor to the
  ordered quantity; a received-anchored tolerance would grow with the excess.

## Examples

- Ordered 100, received 97 → posted 97, variance 3, reason required, no
  approval.
- Ordered 100, received 103 → auto-accept, posted 103, reason required.
- Ordered 100, received 108 → excess 8 > %5; unapproved: posts 100,
  rejected line 8, supplier credit expectation 8; approved: posts 108.
- Ordered 0 (unplanned receipt) → treated as over-receipt above tolerance;
  requires `Manager` approval.

## Invariants (consumers)

- `V11-PUR-001`: each variance case produces exactly one result, one
  authority and one stock/supplier effect.
- Stock quantity changes only with posted receipt quantities.
- No onaysız (unapproved) over-receipt is ever posted.
