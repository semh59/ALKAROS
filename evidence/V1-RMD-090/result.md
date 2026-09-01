# V1-RMD-090 - Seating tolerates stale table version

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi

## Problem

`DualScreenStore.StartOrderAsync` locks the table row `FOR UPDATE`, then — before
any intent check — rejected the seat with
`DualScreenConflictException("Table row version is stale.")` whenever the caller
passed an `ExpectedTableRowVersion` that did not exactly match the current
`row_version`. A waiter whose floor-plan screen was one version behind (a routine
pointer-projector rebuild, another table's reservation, anything) got a spurious
"refresh" error while trying to seat a table that was genuinely `Available`.

## Change

`src/Host/DualScreen/DualScreenStore.cs`:

- Removed the `request.ExpectedTableRowVersion is { } expected && expected != tableRowVersion`
  equality gate and the now-unused `row_version` read from the locked SELECT.
- `ExpectedTableRowVersion` stays in the `StartOrderRequest` contract (API
  compatibility) and keeps its positive-value / requires-table-id validation, but
  is no longer used to gate the seat.
- The seat decision is now made entirely from the fresh state read under the
  `FOR UPDATE` lock, which was already present:
  - `!tableActive` -> "Inactive tables cannot receive an order."
  - `current_bill_id is not null` -> "Table already has an active bill."
  - existing order not `Draft` / on a different table -> "non-editable order"
  - order active on another terminal -> "active on another terminal"
  - `current_status != 'Available'` -> "Only an available table can receive a new order."
  - final atomic bind: `UPDATE ... WHERE current_status = 'Available' AND current_order_id IS NULL AND current_bill_id IS NULL` (0 rows -> "Table changed before the order could be bound.")

Genuine conflicts still reject with a specific message; only the version-mismatch
false positive is gone.

## Tests

`tests/Host/MigrationComposition/DualScreen/DualScreenStoreTests.cs`:

- `TableBoundOrderRejectsStaleOrBusyTablesWithoutCreatingAnotherOrder` rewritten as
  `StaleTableVersionStillSeatsAnAvailableTableButBusyTableIsRejected`:
  stale `ExpectedTableRowVersion` (2 vs real 1) on an `Available` table now
  **seats** (1 order created, table `Occupied`); a second terminal seating the
  now-busy table is still rejected without creating a second order.
- New `NonAvailableTableIsRejectedEvenWithAMatchingVersion`: a `Reserved` table
  with an exactly-matching version is still rejected.

Result (Docker `alkaros-sdk10-rt8` + `alkaros-pg`):

```
dotnet test tests/Host/MigrationComposition --filter FullyQualifiedName~DualScreen
Passed!  - Failed: 0, Passed: 21, Skipped: 0, Total: 21
```

## Related

`V1-RMD-078` (table metadata field-level merge, `NotApplicable`) rationale
updated: generic column merge is an anti-pattern for coupled transactional state;
the real field friction is addressed here via fresh-state intent validation under
`FOR UPDATE`, which matches restaurant-POS industry practice.
