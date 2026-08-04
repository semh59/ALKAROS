# V0-DOM-009 Decision Record — approved

- Task: V0-DOM-009
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:III.15, CORR:C11
- Access date: 2026-08-02
- Result: Approved
- Artifact: `docs/domain/receipt-variance-policy.md`

## Decision summary

- Short delivery: always recorded with actual quantity, no approval; stock
  posts received quantity only.
- Over-receipt ≤ %5 of ordered quantity: auto-accept with mandatory reason.
- Over-receipt > %5: `Manager` approval required; unapproved excess is a
  rejected line with supplier credit expectation, never posted.
- Tolerance anchored to ordered quantity; single rule for all suppliers.
- Rejected quantities get their own goods receipt line; no stock posting.
- Variance decisions are recorded at `received_at`; no later rewrite.

## Verification

- PDF `III.15` (satır 1934-1952) yalnız schema tanımlar; variance politikası
  tanımlı değil — `CORR:C11` boşluğu; politika named business approver ile
  kapatıldı.
