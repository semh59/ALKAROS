# V1-RMD-052 Verification Evidence

- Task ID: V1-RMD-052
- Date: 2026-08-29
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3

## 1. Authoritative Domain Order-to-Bill Bridge

- `src/Host/Experience/Billing/BillingSplitStore.cs`:
  - Uses `Bill.FromOrder` factory method from `ALKAROS.Billing.BillFoundation.Bill` to preserve order metadata, currency, and item snapshots while excluding cancelled items.
  - Added idempotent lookup via `_bills.GetByOrderIdAsync(orderId)` to return existing bill allocations if already created.
  - Added order state guards preventing bill creation from cancelled or rejected orders.
  - Mapped route directly at `/api/v1/terminals/{terminalId:guid}/billing/bills/from-order/{orderId:guid}`.

## 2. PosTerminal Dynamic Floor Plan, Freshness, and Billing Flow

- `src/Clients/PosTerminal/src/App.tsx`:
  - `TableRoute`: dynamic zone selection (`selectedZoneId`, `onSelectZone`), fetching and updating floor plans dynamically on zone switch.
  - `BillingRoute`: dynamic bill/order loading via `orderId` or `billId` URL search parameters, dynamic guest participant allocation modeling.
  - `ExperiencePage`: persistent `lastOnlineSync` tracking to produce accurate `Freshness` status and human-readable last sync timestamps.
- `src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx` and `models.ts`:
  - Propagated `selectedZoneId` and `onSelectZone` through `TableWorkspaceProps`.
- `src/Clients/PosTerminal/src/features/billing/billingApi.ts`:
  - Added `createBillFromOrder(terminalId, orderId, fetcher)`.
