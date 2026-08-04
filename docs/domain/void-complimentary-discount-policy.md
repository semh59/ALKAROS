# Void, Complimentary and Discount Policy — approved decision record

> **Task:** V0-DOM-006
> **Status:** Done
> **Work type:** decision
> **Source basis:** PDF:II.2.5, PDF:II.3.3, PDF:II.5.2, PDF:III.7
> **Access date:** 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

PDF distinguishes the four operations explicitly (`I.24` "Void ≠ Refund",
"Void ≠ Complimentary"; `I.28.1`):

- Void: hazırlanmamış sipariş kaleminin iptali.
- Refund: finansal ödemenin tersine çevrilmesi.
- Waste: hazırlanmış ancak satılamayan ürünün stoktan çıkması.
- Complimentary: ürün teslim edilir, bedel 0 TL olur; yetki/audit gerektirir.

`II.3.3 Bill` allows re-open/void "only if explicitly allowed"; `III.6.2`
order_items carry `discount_amount default 0` and canonical status `Draft,
Active, Cancelled, Waste, Complimentary`; `III.7.2` bill_items carry
canonical `line_type: Sale, Discount, Complimentary, Refund, Waste,
Adjustment`.

## Selected decisions

| Rule | Selected result | Basis |
| --- | --- | --- |
| Void eligibility | Only a not-yet-prepared order item (`kitchen_state NotSent`, order item `Active`) can be voided | PDF `I.28.1` (Void tanımı) |
| Void reason | Every void requires a mandatory reason from a fixed reason catalog (operator error, product unavailable, customer change, duplicate entry); free-text alone is not accepted | Void ≠ Refund (PDF `I.24`); reason catalog keeps audit actionable |
| Void authority | Every void requires an authorized `Manager` role action; no amount threshold | Complimentary requires "yetki/audit" (PDF `I.28.1`); same authority principle extended to void |
| Void audit | `order_status_history` records `old_status`, `new_status`, `reason`, `changed_by`, `changed_at` for every void | PDF `III.6.4` |
| Void fiscal effect | A voided item never appears on a fiscal document; a void after fiscal issuance is a refund path, not a void | Void ≠ Refund (PDF `I.24`) |
| Complimentary | Product is delivered, price becomes 0; always requires `Manager` authority + mandatory reason + audit row | PDF `I.28.1` (Complimentary: yetki/audit gerektirir) |
| Complimentary fiscal effect | `line_type Complimentary` with 0 taxable base; it does not add to `discount_total` | PDF `III.7.2` canonical line_type |
| Discount | Line-level `discount_amount default 0`; a discount is a `Discount` line carrying only the price difference | PDF `III.6.2`/`III.7.2` |
| Discount distribution | Discount distribution across lines is proportional to line totals with per-line round-half-up kuruş rounding (same rule as `V0-CMP-002`); bill `discount_total` is the sum of rounded line discounts | V0-CMP-002 invariant (same-basket same-result) |
| Zero/negative price effects | Every zero/negative price effect (void, comp, discount, adjustment) has one authority rule and one audit rule; no effect is silent | Acceptance invariant; PDF audit-first (`III.1.7`) |
| Waste and Refund | Not defined here; owned by `V0-DOM-003` (refund ledger) and stock domain | Scope boundary |

## Rejected alternatives

- Void without reason or authority — rejected: silent void violates audit-first
  (`III.1.7`) and the "yetki/audit" principle.
- Threshold-based authority (small voids without approval) — rejected: any
  void changes fiscal output; authority is per-operation, not per-amount.
- Comp as negative Sale line — rejected: comp has its own canonical
  `line_type` and 0 base.
- Discount as bill-level post-calculation only — rejected: PDF stores
  per-line `discount_amount`; bill `discount_total` is derived.
- Bill-level discount re-rounding — rejected: per-line rounding invariant
  (V0-CMP-002) keeps lines summing to bill.

## Invariants (consumers)

- `V1-ORD-003`, `V1-BIL-003`: the same item cannot be classified by two
  conflicting operations (void vs refund vs waste vs comp) in the same
  transaction.
- Every zero/negative price effect carries a `reason`, an acting
  `changed_by` with `Manager` authority and an `order_status_history` row.
- A discount changes only `discount_amount`/`discount_total`; it never
  changes unit prices or tax rates.
