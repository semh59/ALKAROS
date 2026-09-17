# Complimentary Line Fiscal Representation — approved decision record

> **Task:** V13-GOV-002
> **Status:** Done
> **Work type:** decision
> **Source basis:** PO:2026-09-17
> **Access date:** 2026-09-17
> **Approver:** Semih — 2026-09-17
> **Decision type:** Business decision (named approver, market-research-driven)

## Selected model

A `Complimentary` (ikram) bill line is **never** a zero-value/invisible
line. It is represented exactly like a `Sale` line, priced at the item's
real unit price, with a discount equal to the full line subtotal (a 100%
discount):

- `discount_amount = quantity * unit_price` (plus any prior discount the
  order item already carried, so a comp applied on top of an existing
  discount still recovers the true original subtotal).
- `net_amount = 0`, `tax_amount = 0`, `gross_amount = 0` — the customer's
  payable stays exactly 0, unchanged from before this decision.
- `line_subtotal` (`net_amount + discount_amount`) therefore equals the
  item's real gross value, not 0.

Enforced as a hard invariant on `BillItem` construction: a `Complimentary`
line whose `discount_amount` does not equal its own subtotal fails
construction (`ArgumentException`), rather than silently producing an
under-reported comp.

## Why

Turkish fiscal register (YN ÖKC) and e-Adisyon reporting expect every sold
item to appear with its real value; a promotional/complimentary give-away
is a **discount event on a real sale**, not a transaction that never
happened. A silent zero-value line hides both the item's real value and
the fact that a discount was applied, understating the business's true
turnover and discount volume to any fiscal or internal report reading the
bill's line items directly — even though the customer's payable total was
already correct either way. `ItemExceptionHandler.ApplyComplimentaryAsync`
(Orders module) already preserves the order item's real price/tax "for tax
records"; this decision makes `BillItem` stop discarding that when it
copies the order item into the Bill.

## Examples

Positive 1: a ₺180 Döner marked Complimentary → `BillItem.DiscountAmount =
180`, `NetAmount = TaxAmount = GrossAmount = 0`, `LineSubtotal = 180`. The
customer's bill total is unaffected; the comp's real value is now visible
in `Bill.Subtotal`/`Bill.DiscountTotal`.

Positive 2: a ₺50 item with a modifier (+₺20) marked Complimentary →
`DiscountAmount = 70` (unit price × quantity plus the modifier delta, via
the order item's own already-modifier-inclusive `NetAmount`), not just the
base ₺50 — modifiers are not lost.

Negative 1 (rejected alternative): silently forcing `NetAmount`/
`TaxAmount`/`GrossAmount` to 0 without recording a matching discount — the
prior, buggy behavior. Rejected: makes every comp invisible in
`Bill.Subtotal`/`Bill.DiscountTotal`, understating both figures.

Negative 2: constructing a `Complimentary` `BillItem` with a partial
discount (e.g. `discount_amount = 30` on a ₺50 line) → rejected at
construction (`ArgumentException`, "must be fully discounted").

## Invariants for consumers

- A `Complimentary` bill line's `discount_amount` always equals its own
  line subtotal (100% discount); its `net_amount`/`tax_amount`/
  `gross_amount` are always 0.
- `Bill.PayableAmount` (sum of `gross_amount`) is unaffected by this
  decision — the customer's total owed does not change.
- `Bill.Subtotal`/`Bill.DiscountTotal` (which sum `LineSubtotal`/
  `DiscountAmount` across items) now include comp'd items' real value —
  any report or UI reading these bill-level totals must expect a comp'd
  item to contribute its real gross value to `Subtotal` and an equal
  amount to `DiscountTotal`, net effect 0 on `PayableAmount`.
- `V13-FSC-*` (fiscal document generation) must emit a comp'd line as a
  real sale line plus a 100% discount line — never omit it.

## Affected tasks

- Implementation (already Done, ahead of this decision record):
  `V1-RMD-228` (`src/Modules/Billing/BillFoundation/BillItem.cs`).
- Consumers: `V13-FSC-001` (fiscal document lifecycle must read this
  representation correctly).

## Acceptance evidence

- `V1-RMD-228`'s own acceptance evidence (42/42 `BillFoundation` tests,
  28/28 `SplitDesign`, 16/16 `Adjustments`, 18/18 real-Postgres `Billing`
  HTTP, 14/14 real-Postgres `Orders/Comp` HTTP) already demonstrates this
  model end to end.
- Decision record with source, approver and affected tasks: above.
