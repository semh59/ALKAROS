# Inventory Cost Basis — approved decision record

> **Task:** V0-DOM-010
> **Status:** Done
> **Work type:** decision
> **Source basis:** PDF:I.22.1, PDF:II.2.10, PDF:III.12, CORR:C9, CORR:C12
> **Access date:** 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

PDF defines the recipe/cost schema (`III.12` recipes, recipe_versions with
`effective_from`/`effective_to`, recipe_ingredients with `waste_factor
default 0` and `unit_code`, unit_conversions with `factor`, and
`recipe_cost_snapshots` with `cost_basis_date`/`calculated_cost`). The PDF
does not bind the quantity computation order or the stock valuation method;
`CORR:C9` (order-of-operations ambiguity, fixed sentence in III.12.3) and
`CORR:C12` (no valuation source) record the gaps.

## Selected decisions

| Rule | Selected result | Basis |
| --- | --- | --- |
| Quantity order-of-operations | Effective consumption = `quantity × (1 + waste_factor)` in the recipe's native `unit_code`, then converted to the stock item's tracked unit via `unit_conversions` as the final step | PDF `III.12.3` fix sentence (CORR:C9) |
| Historical cost source | Recipe cost for a production batch is the `recipe_cost_snapshots` row whose `cost_basis_date` covers the batch `produced_at`; `recipe_version_id` is immutable after batch creation | PDF `I.22.1`, `III.12.5`, `III.13.1` ("must never be changed after batch creation") |
| Valuation method | Moving average per stock item, computed in `numeric(18,4)` weight/quantity units, monetary values `numeric(18,2)` | PDF `III.1.5` (numeric, never float); moving average chosen by business approver |
| Moving-average update | Only posted receipts and posted production-consumption movements update the moving average; estimates and snapshots never do | Historical record keeping (PDF III.37) |
| Rounding | Quantity arithmetic in `numeric(18,4)`; monetary cost in `numeric(18,2)` with round-half-up | PDF `III.1.5` |
| Negative stock | Negative stock is rejected; a consumption posting that would go negative requires a correction movement first | Correction behavior selected by business approver; no silent negative |
| Recipe cost snapshot | `calculated_cost` is regenerated only when the ingredients/cost basis change and is stored with `cost_basis_date`; it is never rewritten after creation | PDF `III.12.5`; immutable history |
| Waste/Complimentary cost effect | `Waste` and `Complimentary` movements reduce stock with the same moving-average unit cost | Same valuation method for all movements |

## Rejected alternatives

- FIFO/LIFO — rejected: moving average matches the restaurant's continuous
  small-batch purchasing and requires no lot tracking the PDF does not model.
- Unit conversion before waste factor — rejected: `CORR:C9` fix sentence
  binds waste factor first in native unit, conversion last.
- Cost from latest purchase price only — rejected: historical
  reproducibility requires `recipe_cost_snapshots` at `cost_basis_date`.
- Rewriting `recipe_cost_snapshots` after creation — rejected: immutable
  history (PDF III.37).
- Negative stock allowed with later correction — rejected: no silent
  negative; correction is a first-class movement.

## Examples

- Recipe calls for `200 g` of an ingredient with `waste_factor 0.05`, stock
  tracked in `kg`: `200 × 1.05 = 210 g`, then `210 / 1000 = 0.21 kg`.
- Batch produced 2026-08-10 uses the snapshot whose `cost_basis_date` is the
  last one `<= 2026-08-10` for the same `recipe_version_id`.
- Receipt of `10 kg @ 40.00 TRY/kg` with prior stock `10 kg @ 30.00` →
  moving average `(10×30 + 10×40) / 20 = 35.00 TRY/kg`.
- Consumption of `0.21 kg @ 35.00` posts `7.35 TRY` cost.

## Invariants (consumers)

- `V11-RCP-002`, `V11-PRD-001`, `V11-PRD-002`, `V11-RPT-001`: the same
  recipe, waste factor, unit conversion, stock history and business date
  produce one reproducible base-unit consumption and one historical cost.
- Moving average is derived from posted movements only; a recomputation on
  the same history yields the same value.
