-- Migration 060: Immutable Stock Movements Ledger (V11-INV-001)
CREATE SCHEMA IF NOT EXISTS inventory;

CREATE TABLE IF NOT EXISTS inventory.stock_movements (
    stock_movement_id UUID PRIMARY KEY,
    stock_item_id UUID NOT NULL REFERENCES inventory.stock_items(id) ON DELETE RESTRICT,
    stock_location_id UUID NOT NULL REFERENCES inventory.stock_locations(id) ON DELETE RESTRICT,
    movement_type VARCHAR(32) NOT NULL,
    direction VARCHAR(16) NOT NULL,
    quantity NUMERIC(14, 4) NOT NULL CHECK (quantity > 0),
    unit_code VARCHAR(32) NOT NULL,
    source_type VARCHAR(64) NOT NULL,
    source_reference_id UUID,
    reason VARCHAR(255),
    created_by UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_stock_movements_item_location
    ON inventory.stock_movements (stock_item_id, stock_location_id);

CREATE INDEX IF NOT EXISTS idx_stock_movements_source
    ON inventory.stock_movements (source_type, source_reference_id);

CREATE INDEX IF NOT EXISTS idx_stock_movements_created_at
    ON inventory.stock_movements (created_at ASC);

-- Immutability enforcement trigger: prohibit UPDATE and DELETE on ledger rows
CREATE OR REPLACE FUNCTION inventory.prevent_stock_movement_mutation()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Stock movements are immutable and cannot be updated or deleted.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_stock_movements_immutable ON inventory.stock_movements;
CREATE TRIGGER trg_stock_movements_immutable
BEFORE UPDATE OR DELETE ON inventory.stock_movements
FOR EACH ROW EXECUTE FUNCTION inventory.prevent_stock_movement_mutation();
