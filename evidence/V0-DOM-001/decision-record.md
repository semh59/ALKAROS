# V0-DOM-001 Decision Record — approved

- Task: V0-DOM-001
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:I.46, PDF:I.46A, PDF:II.5.1-II.5.15, PDF:III.6-III.23,
  PDF:I.8, PDF:I.13, PDF:I.16, PDF:I.27.1, PDF:I.28, PDF:I.28.1, CORR:C29
- Access date: PDF source 2026-07-29; artifact verification 2026-08-02
- Result: Approved
- Artifact: `docs/domain/lifecycle-transition-contracts.md`

## Approved decisions

- Canonical state sets copied unchanged from PDF:I.46A/II.5 (16 machines +
  InventoryMovement MovementType).
- Timeout: 3 retries (2s/5s/15s) then `Unknown`/`ReconciliationRequired` +
  ReconciliationCase (CORR:C29).
- Fiscal trigger: Payment `Approved` → FiscalDocument `Requested`; refund/
  cancel → fiscal refund/cancel pathway (I.28, I.28.1); provider-specific
  ordering to V12-FSC-*/V12-HUG-*.
- Reopen: only `Bill.Reopened` via explicit audited action; all other terminal
  states never reopen; corrections are new domain actions (II.1.3).
- Full per-machine transition matrix (allowed/forbidden, no wildcards) in
  artifact.

## Evidence

- Decision record written to owned surface artifact; status `Done`.
- `python tools/plan-audit/plan_audit_tool.py validate` re-run after closure.
