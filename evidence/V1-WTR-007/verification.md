# V1-WTR-007 Verification Evidence

- Task ID: V1-WTR-007
- Date: 2026-08-29
- Assignee: a04804b8-a15d-498a-9753-7b7c3a0e27b3

## 1. Waiter Host Order Experience Implementation

Created:

- `src/Host/Experience/Orders/OrderManagementContracts.cs`: DTO contracts for table draft orders and submission.
- `src/Host/Experience/Orders/OrderManagementStore.cs`: Thread-safe in-memory/persistence store managing draft lifecycle and optimistic concurrency checks.
- `src/Host/Experience/Orders/OrderManagementEndpoints.cs`: Authoritative REST endpoints under `/api/v1/terminals/{terminalId}/orders` (`/table-draft`, `/{orderId}/submit`, `/table/{tableId}`).
- `tests/Host/Experience/Orders/OrderManagementExperienceTests.cs`: Unit test suite verifying draft creation, total calculations, valid version submission, and stale version conflict rejection.
