-- Migration 117: Physical/cycle counts (V11-INV-008)
-- Records what a manager/staff member actually counted on a shelf. When
-- the counted quantity differs from the current on-hand balance, the
-- resulting adjustment goes through the same guarded-transaction pattern
-- as inventory.ManualAdjustments (StockMovementType.Adjustment,
-- StockMovementSourceType.InventoryAudit) and this row records which
-- movement (if any) resulted.

CREATE TABLE IF NOT EXISTS inventory.stock_physical_counts (
    id UUID PRIMARY KEY,
    stock_item_id UUID NOT NULL REFERENCES inventory.stock_items(id) ON DELETE RESTRICT,
    stock_location_id UUID NOT NULL REFERENCES inventory.stock_locations(id) ON DELETE RESTRICT,
    counted_quantity NUMERIC(14, 4) NOT NULL CHECK (counted_quantity >= 0),
    previous_on_hand_quantity NUMERIC(14, 4) NOT NULL,
    counted_by_user_id UUID NOT NULL,
    notes VARCHAR(255),
    resulting_movement_id UUID REFERENCES inventory.stock_movements(stock_movement_id) ON DELETE RESTRICT,
    counted_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_stock_physical_counts_item_location_counted_at
    ON inventory.stock_physical_counts (stock_item_id, stock_location_id, counted_at);

-- Immutability enforcement trigger, same pattern as inventory.stock_movements.
CREATE OR REPLACE FUNCTION inventory.prevent_physical_count_mutation()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Physical counts are immutable and cannot be updated or deleted.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_stock_physical_counts_immutable ON inventory.stock_physical_counts;
CREATE TRIGGER trg_stock_physical_counts_immutable
BEFORE UPDATE OR DELETE ON inventory.stock_physical_counts
FOR EACH ROW EXECUTE FUNCTION inventory.prevent_physical_count_mutation();
