-- Migration 061: Authoritative On-Hand Stock Balances Projection (V11-INV-002)
CREATE SCHEMA IF NOT EXISTS inventory;

CREATE TABLE IF NOT EXISTS inventory.stock_balances (
    stock_balance_id UUID PRIMARY KEY,
    stock_item_id UUID NOT NULL REFERENCES inventory.stock_items(id) ON DELETE RESTRICT,
    stock_location_id UUID NOT NULL REFERENCES inventory.stock_locations(id) ON DELETE RESTRICT,
    on_hand_quantity NUMERIC(14, 4) NOT NULL DEFAULT 0,
    reserved_quantity NUMERIC(14, 4) NOT NULL DEFAULT 0,
    available_quantity NUMERIC(14, 4) NOT NULL DEFAULT 0,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    row_version INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT uq_stock_balances_item_location UNIQUE (stock_item_id, stock_location_id)
);

CREATE INDEX IF NOT EXISTS idx_stock_balances_item
    ON inventory.stock_balances (stock_item_id);

CREATE INDEX IF NOT EXISTS idx_stock_balances_location
    ON inventory.stock_balances (stock_location_id);
