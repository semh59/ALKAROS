DROP INDEX IF EXISTS kitchen.ix_kitchen_print_deliveries_recoverable;

ALTER TABLE kitchen.physical_print_deliveries
    DROP COLUMN IF EXISTS state_changed_at;
