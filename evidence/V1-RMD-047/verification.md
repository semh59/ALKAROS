# V1-RMD-047 Verification Evidence

- Task ID: V1-RMD-047
- Date: 2026-08-29
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3

## 1. Cashier UI Real Backend Dispatch

- Updated `dispatchOrderToKitchen()` in `src/Clients/Cashier/wwwroot/cashier-app.js` to execute real `POST /api/v1/terminals/{terminalId}/orders/table-draft` request.
- Eliminated all silent in-memory fake success assumptions.
- Handled network errors and HTTP non-2xx responses with fail-closed user alerts.

## 2. Waiter PWA Fallback Cleanup

- Removed `fallbackZones`, `fallbackCatalog`, and `fallbackTables` from `src/Clients/WaiterPwa/wwwroot/waiter-app.js`.
- Configured clean empty/offline state when Host API is unreachable, showing an offline status banner.
