-- Migration 061: Authoritative Portion Reservation Lifecycle (V11-RSV-001)
CREATE SCHEMA IF NOT EXISTS inventory;

CREATE TABLE IF NOT EXISTS inventory.portion_reservations (
    id UUID PRIMARY KEY,
    order_id UUID NOT NULL,
    order_item_id UUID NOT NULL,
    stock_item_id UUID NOT NULL REFERENCES inventory.stock_items(id) ON DELETE RESTRICT,
    stock_location_id UUID NOT NULL REFERENCES inventory.stock_locations(id) ON DELETE RESTRICT,
    quantity NUMERIC(14, 4) NOT NULL CHECK (quantity > 0),
    unit_code VARCHAR(32) NOT NULL,
    status VARCHAR(32) NOT NULL CHECK (status IN ('Reserved', 'Released', 'Consumed', 'Waste')),
    version INT NOT NULL DEFAULT 1 CHECK (version >= 1),
    idempotency_key VARCHAR(128) NULL,
    reserved_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    transitioned_at TIMESTAMPTZ NULL,
    transition_reason VARCHAR(255) NULL,
    created_by UUID NOT NULL,
    transitioned_by UUID NULL,
    metadata JSONB NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_portion_reservations_idempotency
    ON inventory.portion_reservations (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_portion_reservations_order_item
    ON inventory.portion_reservations (order_id, order_item_id);

CREATE INDEX IF NOT EXISTS idx_portion_reservations_stock
    ON inventory.portion_reservations (stock_item_id, stock_location_id, status);

CREATE INDEX IF NOT EXISTS idx_portion_reservations_status
    ON inventory.portion_reservations (status);
