# V1-RMD-041 Verification Evidence

- Task ID: V1-RMD-041
- Date: 2026-08-29
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3

## 1. Bill Split Production Navigation and Route Integration

- Connected `BillSplitWorkspace` directly into `App.tsx` navigation (`/billing`, `Hesap`, symbol: `÷`).
- Implemented `BillingRoute` handling loaded/ready/error/offline/stale state transitions, split saving (Equal, ByItem, ByAmount), and clearing allocations.
- Wired terminal ID and active bill context with `createBillingSplitClient`.
