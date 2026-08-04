# V0-DOM-010 Decision Record — approved

- Task: V0-DOM-010
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:I.22.1, PDF:II.2.10, PDF:III.12, CORR:C9, CORR:C12
- Access date: 2026-08-02
- Result: Approved
- Artifact: `docs/domain/inventory-cost-basis.md`

## Decision summary

- Quantity order: `quantity × (1 + waste_factor)` in native unit, then
  `unit_conversions` to stock unit (CORR:C9 fix sentence, PDF III.12.3).
- Historical cost: `recipe_cost_snapshots` row covering the batch
  `produced_at` via `cost_basis_date`; `recipe_version_id` immutable after
  batch creation (PDF I.22.1, III.12.5, III.13.1).
- Valuation: moving average per stock item; `numeric(18,4)` quantities,
  `numeric(18,2)` money, round-half-up; negative stock rejected; snapshots
  never rewritten.

## Verification

- PDF satırları: I.22.1 (historical cost, 436-439), III.12.3-5 (recipe
  schema, 1839-1850), III.13.1 (immutable recipe_version_id, 1853-1860),
  C9 fix sentence (2716-2719).
