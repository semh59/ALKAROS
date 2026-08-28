# V1-RMD-035 Deep Code Audit Remediation - Verification Evidence

## Scope and Items Remediated

1. **DualScreen Store Discount Math Synchronization**
   - File: `src/Host/DualScreen/DualScreenStore.cs`
   - Fixed `net_amount = round(unit_price * @quantity - discount_amount, 2)` in `MutateExistingItemAsync`.
   - Verified that `gross_amount = net_amount + tax_amount` DB CHECK constraint (`ck_order_items_nonnegative_amounts`) is satisfied and order subtotal in `RecalculateOrderAsync` is mathematically exact without double-counting discounts.

2. **Bill State Machine Reopened Deadlock Resolution**
   - File: `src/Modules/Billing/BillFoundation/Bill.cs`
   - Added `BillState.Reopened` to `PartiallyAllocated`, `Allocated`, and `Cancelled` target transitions in `CanTransitionTo`.
   - Verified that reopened bills can transition to allocation and closure workflows without deadlock.

3. **BillItem Modifier & Discount Subtotal Alignment**
   - File: `src/Modules/Billing/BillFoundation/BillItem.cs`
   - Adjusted `lineSubtotal` computation and `LineSubtotal` property to correctly reflect modifier totals and discounts.
   - Verified that order items with modifiers and discounts convert to bill items with zero monetary drift or exceptions.

4. **TableTransfer Concurrency Lock Ordering**
   - File: `src/Modules/Tables/TableTransfer/PostgresTableTransferRepository.cs`
   - Implemented canonical Guid ascending `FOR UPDATE` lock ordering across `SourceTableId` and `TargetTableId`.
   - Verified elimination of cyclic deadlock (PostgreSQL 40P01) during concurrent reverse table transfers.

5. **AuditSanitizer Malformed JSON Fallback Redaction**
   - File: `src/Modules/Audit/EventStore/IAuditSanitizer.cs`
   - Added `FallbackSanitizeText` regex redaction on JSON parse failure and wrapped unparsed payloads into a valid JSON string object.
   - Verified that unparsed/raw sensitive strings never bypass redaction and never crash PostgreSQL `jsonb` columns.

6. **IdempotencyKeyStore Aborted Concurrent Transaction Recovery**
   - File: `src/BuildingBlocks/Idempotency/IdempotencyKeyStore.cs`
   - Wrapped `ExecuteAsync` in an attempt loop that rolls back and retries mutation claim when a previous concurrent uncommitted record disappears.
   - Verified that aborted transient requests do not cause subsequent requests to fail with invalid operation errors.

7. **Table State Machine Transition Matrix Completeness**
   - File: `src/Modules/Tables/TableLifecycle/Table.cs`
   - Added `Reserved -> Occupied`, `Occupied -> Cleaning`, and `Cleaning -> OutOfService` transitions.
   - Updated unit tests in `TableDomainTests.cs` to assert canonical transition behavior.
