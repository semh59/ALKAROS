# V1-RMD-089 - Orders scale index migration result

- Date: 2026-09-01
- Owner: claude-code-019uHsMymyuC1tZ5DHtmWsRi
- Environment: disposable PostgreSQL 18 (Docker `postgres:18`) on the tuned config
  (V1-RMD-088), schema-only clone of the live `alkaros` database seeded with
  1,000,000 orders + 3,000,000 order_items (~1.1 GB).

## Migration

`database/migrations/V1/V1-RMD-089/041-orders-scale-indexes.up.sql` /`.down.sql`:

```sql
CREATE INDEX IF NOT EXISTS ix_orders_created_at ON orders.orders (created_at);
CREATE INDEX IF NOT EXISTS ix_orders_table_open
    ON orders.orders (table_id, created_at DESC)
    WHERE status IN ('Draft','Submitted','PendingConfirmation','Accepted','Preparing','Ready','Served');
```

Composition wiring:

- `database/MigrationComposition/order.json`: `041` entry (`phase B`, `tables ["orders"]`); `phaseBRange.max` 040 -> 041.
- `src/Host/Composition/Migrations/MigrationManifest.cs`: `PhaseBMax` 040 -> 041.
- `tests/Host/MigrationComposition/Manifest/ManifestTests.cs`: `RuntimeManifestIds` += `"041"`; `Migrations.Count` 39 -> 40; `LastEntryTables` `["products"]` -> `["orders"]`; the phase-B out-of-range test id 041 -> 042.

## Round-trip (migration-roundtrip.log)

```
apply up      -> ix_orders_created_at + ix_orders_table_open created (2)
apply up again -> IF NOT EXISTS, no-op (idempotent)
apply down    -> both dropped (0)
apply up again -> both re-created (2)
MIGRATION_041_ROUNDTRIP_OK
```

## Before / after EXPLAIN at 1M rows (explain-before-after.log)

| Query | Before 041 | After 041 |
| --- | --- | --- |
| Business-day revenue: `WHERE created_at >= now()-'1 day' AND created_at < now() AND status='Completed'` | Parallel Seq Scan, **118 ms** | Bitmap Index Scan on `ix_orders_created_at`, **14.0 ms** (~8x) |
| Most recent open order for a table: `WHERE table_id = ? AND status IN (open states) ORDER BY created_at DESC LIMIT 1` | Parallel Seq Scan, **136 ms** | Index Scan on `ix_orders_table_open`, **0.16 ms** (~850x) |
| Control - load order by `order_id` (PK) | 0.10 ms | 0.10 ms (unchanged) |

## Tests (dotnet-composition-tests.log)

- `dotnet test tests/Host/MigrationComposition` : **99 passed, 0 failed**
- `dotnet test tests/Host/Experience/KitchenOperations` (applies migration 041 to
  its real test database): **4 passed, 0 failed**

## Scope note

Only `orders.orders` needed new indexes. The scale probe confirmed
`kitchen_tickets` already has `ix_kitchen_tickets_station_status` and
`ix_kitchen_tickets_order_id`, and `audit.audit_events` already has
`(aggregate_type, aggregate_id, occurred_at)`, `(correlation_id)` and
`(occurred_at)` - no gaps there. Unbounded growth of `order_status_history`,
`idempotency_keys` and audit events over months of operation is a data-retention
concern, tracked separately.

## Deployment note

`041` uses plain `CREATE INDEX` (migrations run inside the composition
transaction). On a database that is already large at upgrade time, an operator
should build the two indexes with `CREATE INDEX CONCURRENTLY` by hand during a
maintenance window instead, to avoid holding a write lock on `orders`.
