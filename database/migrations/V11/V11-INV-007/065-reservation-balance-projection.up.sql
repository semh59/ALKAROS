-- Migration 065: Reserved and Available Stock Projection (V11-INV-007)
CREATE SCHEMA IF NOT EXISTS inventory;

-- Table to track applied reservation lifecycle effects for projection idempotency
CREATE TABLE IF NOT EXISTS inventory.reservation_balance_applied_events (
    id UUID PRIMARY KEY,
    reservation_id UUID NOT NULL,
    event_type VARCHAR(32) NOT NULL CHECK (event_type IN ('Reserved', 'Terminal')),
    terminal_status VARCHAR(32) NULL CHECK (terminal_status IS NULL OR terminal_status IN ('Released', 'Consumed', 'Waste')),
    stock_item_id UUID NOT NULL REFERENCES inventory.stock_items(id) ON DELETE RESTRICT,
    stock_location_id UUID NOT NULL REFERENCES inventory.stock_locations(id) ON DELETE RESTRICT,
    quantity NUMERIC(14, 4) NOT NULL CHECK (quantity > 0),
    applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_reservation_applied_event UNIQUE (reservation_id, event_type)
);

CREATE INDEX IF NOT EXISTS idx_reservation_applied_events_lookup
    ON inventory.reservation_balance_applied_events (reservation_id);

CREATE INDEX IF NOT EXISTS idx_portion_reservations_active_agg
    ON inventory.portion_reservations (stock_item_id, stock_location_id, quantity)
    WHERE status = 'Reserved';
