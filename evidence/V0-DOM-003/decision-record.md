# V0-DOM-003 Decision Record — approved

- Task: V0-DOM-003
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:II.2.6, PDF:II.3.4-II.3.5, PDF:II.5.3, PDF:III.8
- Access date: PDF source 2026-07-29; artifact verification 2026-08-02
- Result: Approved
- Artifact: `docs/domain/refund-ledger.md`

## Approved decisions

- Immutable `payment_reversals` ledger; existing payments/allocations never
  mutated; no negative allocations.
- Partial refund → dedicated `PartiallyRefunded` Payment state (does not stay
  `Approved`); full refund → `Refunded`.
- Cumulative limit: sum(reversals) ≤ payment amount; Bill net paid =
  sum(payments) - sum(reversals) ≥ 0.
- Double refund forbidden by immutability; idempotency key per refund.
- Fiscal refund/cancel linkage per PDF:I.28.1 (V12-HUG-003).

## Evidence

- Decision record written to owned surface artifact; status `Done`.
- `python tools/plan-audit/plan_audit_tool.py validate` re-run after closure.
