# V1-RMD-046 Verification Evidence

- Task ID: V1-RMD-046
- Date: 2026-08-29
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3

## 1. Removed In-Memory ConcurrentDictionary

- Replaced process-local `ConcurrentDictionary` in `src/Host/Experience/Orders/OrderManagementStore.cs` with authoritative PostgreSQL persistence using `NpgsqlDataSource` and `IOrderRepository`.
- Bound order draft creation and updates to `orders.orders` and `orders.order_items` tables with optimistic concurrency (`row_version`).
- Updated `table_mgmt.tables.current_order_id` and table status atomically.

## 2. Integrated into Host Composition Root

- Registered `AddOrderManagementExperience` in `src/Host/DualScreen/DualScreenApplication.cs`.
- Mapped `MapOrderManagementApi()` routes under `/api/v1/terminals/{terminalId:guid}/orders`.
