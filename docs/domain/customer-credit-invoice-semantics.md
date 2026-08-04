# Customer Credit and Invoice Semantics — approved decision record

> **Task:** V0-DOM-007
> **Status:** Done
> **Work type:** decision
> **Source basis:** PDF:II.2.15, PDF:II.3.11, PDF:III.18, CORR:C3
> **Access date:** 2026-08-02
> **Approver:** Semih — 2026-08-03
> **Decision type:** Business decision (named business approver)

PDF `II.3.11` defines CustomerAccount as the restaurant-side receivable
relationship used for periodic invoicing and deferred payment. `III.18.3`
defines `account_transactions` with canonical `transaction_type`:
`Charge, Payment, Invoice, Credit, Debit, Adjustment, Refund` and — the
`CORR:C3` fix — a `direction` generated column that can never contradict
`transaction_type`. `Adjustment` carries its sign in `amount`.

## 1. Core Model

Customer credit: a Bill charge deferred to CustomerAccount, settled via
periodic Invoice.

1. Bill → CustomerAccount charge (receivable created, `transaction_type
   Charge`, `direction Debit`)
2. CustomerAccount → Payment (receivable reduced, `Payment`, `Credit`)
3. Periodic Invoice aggregates charges/payments for a period (`Invoice`
   transactions carry the invoice reference)
4. Invoice → FiscalDocument (fiscalized)

## 2. Balance formulas (single reproducible formula)

```text
receivable_balance = opening_balance + SUM(Charge) + SUM(Invoice)
                     + SUM(Debit) + SUM(Adjustment signed by amount)
                     - SUM(Payment) - SUM(Credit) - SUM(Refund)

invoice_balance    = SUM(charges_in_period) - SUM(payments_in_period)
```

- `direction` is never written by the application; it is derived by the
  generated column (`III.18.3`). `Adjustment` has no fixed direction; its
  sign is carried by `amount` and must match the transaction's note.
- `current_balance` (`III.18.2`) is a cached projection; `account_transactions`
  rows are the authoritative ledger. Snapshots (`III.18.4`) store the
  balance per `snapshot_date`.

## 3. Invariants (consumers V13-ACC-001, V13-ACC-003, V13-INV-001)

1. **No double count**: a charge appears in exactly one invoice period;
   `Invoice` transactions never re-create the underlying charges.
2. **Balance closure**: `receivable_balance` equals `SUM` of unpaid invoice
   totals (or the explicit difference is a tracked adjustment).
3. **Invoice-fiscal linkage**: every issued invoice has a corresponding
   `FiscalDocument`.
4. **Durable receivable source (C23)**: collection independent of a bill is
   a `Payment`/`Credit`/`Refund` `account_transactions` row (not a bill
   allocation); the reconciliation chain is
   transaction → account snapshot → invoice → fiscal document.
5. **No silent sign change (C3)**: `amount` is always non-negative in
   storage and the `direction`/`transaction_type` pair defines the balance
   effect; an `Adjustment` with negative `amount` requires a note and
   `created_by`.
6. **Handler registration (C26)**: the customer-account handler is
   registered in the V1.3 module registry and participates in the fiscal
   closure chain before any fiscal document is finalized.

## 4. Examples

- Period with charges `100 + 150`, payments `80`, invoice issued `170`:
  `receivable_balance = 100 + 150 - 80 = 170`; `invoice_balance = 170`;
  closure holds, no double count.
- Adjustment `-20` on invoice `170`: `receivable_balance = 150`; the
  adjustment is a signed `Adjustment` row with note and creator.
- Refund `30`: `receivable_balance = 120`; refund is `Credit`, never a
  reversal of the payment row.

## 5. Rejected alternatives

- Storing `direction` as an ordinary column — rejected: `III.18.3` binds the
  generated column; an independent column would drift (CORR:C3).
- Bill-only collection model — rejected: `C23` requires durable receivable
  rows independent of bills.
- Invoice re-charging underlying charges — rejected: double count
  (invariant 1).

## Task status

- Status: `Done` — decision approved by named business approver on
  2026-08-03; dependencies `V0-DOM-003`, `V0-CMP-002` closed in this batch.
