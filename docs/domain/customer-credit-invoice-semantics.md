# Customer Credit and Invoice Semantics

> **Task:** V0-DOM-007
> **Status:** Blocked
> **Assignee:** codex-v0-dom-007
> **Work type:** decision
> **Source basis:** PDF:II.2.15, PDF:II.3.11, PDF:III.18, CORR:C3
> **Date:** 2026-07-30

## 1. Core Model

Customer credit: Bill charge deferred to CustomerAccount, settled via periodic Invoice.

### Flow

1. Bill → CustomerAccount charge (receivable created)
2. CustomerAccount → Payment (receivable reduced)
3. Periodic Invoice → aggregates charges/payments for a period
4. Invoice → FiscalDocument (fiscalized)

### Balance Formula

```text
receivable_balance = SUM(charges) - SUM(payments) - SUM(adjustments)
invoice_balance = SUM(charges_in_period) - SUM(payments_in_period)
```

## 2. Invariants

1. **No double count**: A charge appears in exactly one invoice period.
2. **Balance closure**: receivable_balance MUST equal SUM of unpaid invoice totals.
3. **Invoice-fiscal linkage**: Every issued invoice MUST have a corresponding FiscalDocument.

## 3. Affected Tasks

- V13-ACC-001, V13-ACC-003, V13-INV-001
