# V0-DAT-002 Decision Record — approved

- Task: V0-DAT-002
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:II.5.1-II.5.15, PDF:III.3-III.40, PDF:II.13-II.15, PDF:III.29-III.40, CORR:C2, CORR:C7
- Access date: PDF source 2026-07-29; artifact verification 2026-08-02
- Result: Approved
- Artifact: `docs/data/canonical-value-catalog.md`

## Approved decisions

- Full canonical catalog: 16 lifecycle machines (+ InventoryMovement
  MovementType), C7 discriminator lists for 5 columns, 21 other PDF-defined
  value lists.
- `PartiallyRefunded` added to Payment set (V0-DOM-003, 2026-08-03) — only
  approved addition to PDF sets.
- CORR:C2: `NotReserved` spans `Draft`, `Submitted`, `PendingConfirmation`
  uniformly; no fourth value.
- Provider-scoped/deferred fields (attempts, purchasing, QR, backup, health,
  printers status) explicitly excluded from internal enums.

## Evidence

- Decision record written to owned surface artifact; status `Done`.
- `python tools/plan-audit/plan_audit_tool.py validate` re-run after closure.
