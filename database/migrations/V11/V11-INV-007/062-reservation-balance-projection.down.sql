-- Migration 062: Rollback Reserved and Available Stock Projection (V11-INV-007)
DROP INDEX IF EXISTS inventory.idx_portion_reservations_active_agg;
DROP INDEX IF EXISTS inventory.idx_reservation_applied_events_lookup;
DROP TABLE IF EXISTS inventory.reservation_balance_applied_events;
