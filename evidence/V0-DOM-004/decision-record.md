# V0-DOM-004 Decision Record — approved

- Task: V0-DOM-004
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:I.11-I.15, PDF:II.2.6, PDF:II.3.4-II.3.5, PDF:II.5.3, PDF:III.8, CORR:C4
- Access date: PDF source 2026-07-29; artifact verification 2026-08-02
- Result: Approved
- Artifact: `docs/domain/payment-allocation-integrity.md`

## Approved decisions

- Immutable `payment_allocations` (amount > 0); no negative allocation
  anywhere (CORR:C4).
- Same-bill invariant: allocation.bill_id = payment.bill_id.
- Currency equality across allocation/payment/bill.
- Remaining-amount cap per bill; over-allocation rejected.
- Overpayment/change allowed for ALL tender types (not only cash); surplus
  recorded in separate `payment.change_amount`, never allocated.
- Idempotency key per allocation; duplicate replay returns existing row.
- Refunds via immutable `payment_reversals` (V0-DOM-003); net paid never
  negative.

## Evidence

- Decision record written to owned surface artifact; status `Done`.
- `python tools/plan-audit/plan_audit_tool.py validate` re-run after closure.
