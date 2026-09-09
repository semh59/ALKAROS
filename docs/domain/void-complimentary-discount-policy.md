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

## Amendment

- **Date:** 2026-09-04
- **Approver:** Semih (named business approver)
- **Change:** The original record's "Void eligibility" row (line 29) treats
  "sent to kitchen" as an absolute wall: a not-yet-prepared item may be
  voided outright, a sent item may not be voided at all (only refunded,
  after fiscal issuance). Shipped V1 restaurants span esnaf lokantası, cafe,
  fine dining and fast food, whose kitchens differ enormously in how
  meaningful "already prepared" is (a lokanta's steam-table items are
  effectively always "prepared"; a fast-food line voids sent items
  routinely; a fine-dining kitchen treats it as exceptional). A single
  hard-coded wall cannot fit all of them. This amendment keeps the wall as
  the *default* (unchanged in-scope: `Active` + `KitchenState = NotSent` is
  a free, unauthorized void) and adds a third state, gated by
  `docs/domain/authorization-model.md`'s `bills.void` grant permission
  rather than an unconditional block:
  - **A sent-but-not-yet-served item may now be voided**, requiring the
    `bills.void` permission (held outright by supervisor/manager; a grant
    for waiter/cashier, resolved by the same policy-engine /
    auto-within-limit machinery as every other grant-class permission —
    so a fast-food deployment can tune its policy to auto-approve within a
    limit, and a fine-dining deployment can require a manager every time,
    without a code branch per venue type). Voiding at this stage cancels
    the matching kitchen ticket item too (a kitchen that already started
    preparing the item is told to stop) and, if the item was already
    reflected on an open `Bill`, removes that `BillItem` and records a
    `BillLineType.Waste` line instead of `Sale` (the enum value already
    existed in the canonical `line_type` catalog per `III.7.2`; this is its
    first real producer) — so reporting can distinguish "never made"
    (void, no cost) from "made but not sold" (waste, real cost) from
    "made, delivered, given away" (comp, `line_type Complimentary`,
    unchanged).
  - This entire path is inert unless a deployment has turned on kitchen
    live-sync (a new Settings-module toggle, `Global` scope, default off —
    see the requester-side wiring task cluster). With the toggle off,
    `OrderItem.KitchenState` never leaves `NotSent`/`Cancelled` (as today),
    so the original wall is the only reachable behaviour and existing
    deployments see no change.
  - A void after fiscal issuance remains a refund, not a void — unchanged
    (line 33).
- **Rationale:** The wall was correct as a *default* but wrong as a
  universal constant — it assumed one kitchen-timing model. Gating the
  exception behind the existing grant/policy engine (rather than a new
  bespoke rule) means the authority question ("should this be allowed
  right now") is answered the same way every other grant-class permission
  in this system is answered: by policy, delegation, or a manager, tunable
  per deployment — not by a fixed clock-based wall in code.
- **Affected tasks:** the requester-side wiring task cluster spawned from
  `V1-IAM-026` (Settings toggle, Kitchen→Orders state sync, waiter
  ready-notification, and the three void/comp/discount endpoints).
- **Not changed:** void reason catalog, void audit row shape, complimentary
  rules, discount rules, and the refund boundary (all lines above except
  29) are unchanged.

## Amendment (2026-09-09)

- **Date:** 2026-09-09
- **Approver:** Semih (named business approver)
- **Change:** The original record's Waste definition (line 16, "hazırlanmış
  ancak satılamayan ürünün stoktan çıkması") and the 2026-09-04 amendment's
  own "waste" handling for a sent-but-unserved void both implicitly assumed
  a sent item had already been *prepared* by the time anyone would void it.
  `V1-RMD-143` (2026-09-09) started actually decrementing real inventory at
  Order Accept — not at kitchen prep start — which broke that assumption:
  `KitchenState.Sent` only means the ticket reached the kitchen, not that
  an ingredient was ever touched. Voiding a `Sent` item and still calling
  its stock cost "Waste" would permanently lose inventory for food that was
  never made. This amendment splits the sent-but-unserved void's stock
  effect by `KitchenState`:
  - **`Sent`** (ticket dispatched, kitchen has not started): the item's own
    Accept-time stock consumption is reversed — a real `StockMovementType.
    Reversal` movement, found by the exact order item id
    (`OrderStockConsumptionService`'s own movements are keyed per item, not
    per order, precisely so this lookup is unambiguous) — restoring
    `on_hand_quantity` to what it was before Accept. The bill line (if any)
    is still converted to `Waste` for billing purposes (the customer owes
    nothing), even though nothing was actually wasted physically — no
    canonical `line_type` exists for "voided after being billed but before
    prep started", and reusing `Waste` there is a labeling compromise, not
    a claim that ingredients were lost.
  - **`Preparing`/`Ready`** (kitchen has started or finished): unchanged —
    stock stays consumed, matching the original Waste definition for a
    genuinely prepared item.
- **Rationale:** The original amendment's "waste" framing predates real
  stock consumption existing at all (Accept had no inventory effect before
  V1-RMD-143) — it could not have anticipated this distinction. Reversing a
  Sent item's stock is strictly more accurate than leaving it consumed, and
  costs nothing the domain does not already support: `StockMovementType.
  Reversal` and `IStockMovementReversalService` (`V11-INV-003`) already
  existed, fully built and tested, with zero callers until this amendment.
- **Affected tasks:** `V1-RMD-143` (`SentItemVoidStore`'s own restore step;
  `OrderStockConsumptionService`'s movements now key `sourceReferenceId` on
  the order item, not the order, specifically to make this lookup exact).
- **Not changed:** everything else in this document, including the
  `Preparing`/`Ready` void path's own existing Waste/bill-conversion
  behaviour and every rule not about stock.
