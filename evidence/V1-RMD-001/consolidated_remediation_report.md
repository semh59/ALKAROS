# V1-RMD-001 Consolidated Remediation Evidence

- Task ID: V1-RMD-001
- Status: Done
- Assignee: Antigravity-v1-rmd-001
- Date: 2026-08-16

## Executed Remediations

1. **TransactionScope Rollback Error Aggregation**:
   - Updated `TransactionScope.cs` to attempt rollback across all enlisted resources with `CancellationToken.None`.
   - Aggregates any errors into `AggregateException` to guarantee all resources execute rollback.
   - Verified via unit test `MultipleRollbackFailuresAggregateAllExceptions`.

2. **Ambient Transaction Join Cancellation Propagation**:
   - Updated `TransactionContext.cs` to explicitly invoke `cancellationToken.ThrowIfCancellationRequested()` when joining an active ambient transaction scope.

3. **Transaction Outbox Post-Commit Dispatch Wake-Up**:
   - Added `onCommitted` hook and `NotifyCommitted()` method in `TransactionOutboxResource.cs`.
   - Called `resource.NotifyCommitted()` in `TransactionOutbox.RunAsync` strictly after transaction commit.

4. **Product Non-Negative Price Validation & Check Constraint**:
   - Added validation `if (currentPrice is < 0) throw new ArgumentOutOfRangeException(...)` in `Product.cs`.
   - Created migration `014-catalog-current-price-bound.up.sql` and `014-catalog-current-price-bound.down.sql` with check constraint `chk_products_current_price_nonnegative`.
   - Verified via domain tests and repository database check constraint tests.

5. **Migration Composition Manifest Tables Fix**:
   - Added `product_modifier_groups` to migration `006` tables manifest in `database/MigrationComposition/order.json`.

6. **Host Program Entry Point Documentation**:
   - Updated `src/Host/Program.cs` doc comment to accurately reflect application entry point.

## Verification Results

- `dotnet build ALKAROS.slnx`: 0 Warnings, 0 Errors.
- `dotnet test ALKAROS.slnx` (486 tests against PostgreSQL 18): **486/486 Passed (0 Failed)**.
- `python -B tools/plan-audit/plan_audit_tool.py validate`: 0 Errors, 0 Warnings.
