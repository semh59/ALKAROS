ALTER TABLE kitchen.physical_print_deliveries
    ADD COLUMN state_changed_at TIMESTAMPTZ NOT NULL DEFAULT now();

CREATE INDEX ix_kitchen_print_deliveries_recoverable
    ON kitchen.physical_print_deliveries (state_changed_at)
    WHERE status IN ('InFlight', 'ReprintInFlight');
