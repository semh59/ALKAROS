# V0-DOM-007 Decision Record — approved

- Task: V0-DOM-007
- Approver: Semih
- Approval date: 2026-08-03
- Source basis: PDF:II.2.15, PDF:II.3.11, PDF:III.18, CORR:C3
- Access date: 2026-08-02
- Result: Approved
- Artifact: `docs/domain/customer-credit-invoice-semantics.md`

## Decision summary

- Single receivable formula; `direction` generated column never written by
  application (CORR:C3 fix, PDF III.18.3 lines 2076-2104).
- Durable receivable source independent of bills (C23) with reconciliation
  chain: transaction → snapshot → invoice → fiscal document.
- Customer-account handler registered in V1.3 registry + fiscal closure
  chain (C26).
- No invoice double count; adjustment sign carried by amount with note.

## Verification

- PDF satırları: II.3.11 (1084-1086), III.18.2-4 (2068-2108), direction
  generated column (2084-2104).
