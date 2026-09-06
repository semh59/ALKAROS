-- Migration 063: Traceable Waste Recording (V11-INV-006)
CREATE SCHEMA IF NOT EXISTS inventory;

CREATE TABLE IF NOT EXISTS inventory.waste_records (
    id UUID PRIMARY KEY,
    stock_movement_id UUID NOT NULL UNIQUE REFERENCES inventory.stock_movements(stock_movement_id) ON DELETE RESTRICT,
    stock_item_id UUID NOT NULL REFERENCES inventory.stock_items(id) ON DELETE RESTRICT,
    stock_location_id UUID NOT NULL REFERENCES inventory.stock_locations(id) ON DELETE RESTRICT,
    waste_source VARCHAR(64) NOT NULL,
    source_reference_id UUID NULL,
    idempotency_key VARCHAR(128) NULL,
    quantity NUMERIC(14, 4) NOT NULL CHECK (quantity > 0),
    unit_code VARCHAR(32) NOT NULL,
    normalized_quantity NUMERIC(14, 4) NOT NULL CHECK (normalized_quantity > 0),
    tracking_unit_code VARCHAR(32) NOT NULL,
    waste_reason TEXT NOT NULL CHECK (length(trim(waste_reason)) > 0),
    recorded_by UUID NOT NULL,
    recorded_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    metadata JSONB NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_waste_records_idempotency
    ON inventory.waste_records (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

CREATE INDEX IF NOT EXISTS idx_waste_records_item_location
    ON inventory.waste_records (stock_item_id, stock_location_id);

CREATE INDEX IF NOT EXISTS idx_waste_records_source
    ON inventory.waste_records (waste_source, source_reference_id);

CREATE INDEX IF NOT EXISTS idx_waste_records_recorded_at
    ON inventory.waste_records (recorded_at);

-- Immutability enforcement trigger: prohibit UPDATE and DELETE on waste_records rows
CREATE OR REPLACE FUNCTION inventory.prevent_waste_record_mutation()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Waste records are immutable audit records and cannot be updated or deleted.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_waste_records_immutable ON inventory.waste_records;
CREATE TRIGGER trg_waste_records_immutable
BEFORE UPDATE OR DELETE ON inventory.waste_records
FOR EACH ROW EXECUTE FUNCTION inventory.prevent_waste_record_mutation();
