CREATE SCHEMA IF NOT EXISTS production;

CREATE TABLE IF NOT EXISTS production.production_outputs (
    production_output_id UUID PRIMARY KEY,
    production_batch_id UUID NOT NULL REFERENCES production.production_batches(production_batch_id) ON DELETE CASCADE,
    stock_item_id UUID NULL REFERENCES inventory.stock_items(id) ON DELETE SET NULL,
    stock_location_id UUID NOT NULL REFERENCES inventory.stock_locations(id) ON DELETE RESTRICT,
    quantity NUMERIC(14, 4) NOT NULL CHECK (quantity > 0),
    unit_code VARCHAR(32) NOT NULL DEFAULT 'portion',
    stock_movement_id UUID NULL REFERENCES inventory.stock_movements(stock_movement_id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_production_outputs_batch
    ON production.production_outputs (production_batch_id);

CREATE TABLE IF NOT EXISTS production.production_consumptions (
    production_consumption_id UUID PRIMARY KEY,
    production_batch_id UUID NOT NULL REFERENCES production.production_batches(production_batch_id) ON DELETE CASCADE,
    stock_item_id UUID NOT NULL REFERENCES inventory.stock_items(id) ON DELETE RESTRICT,
    stock_location_id UUID NOT NULL REFERENCES inventory.stock_locations(id) ON DELETE RESTRICT,
    quantity NUMERIC(14, 4) NOT NULL CHECK (quantity > 0),
    unit_code VARCHAR(32) NOT NULL,
    waste_factor NUMERIC(8, 4) NOT NULL DEFAULT 0 CHECK (waste_factor >= 0),
    native_quantity NUMERIC(14, 4) NOT NULL CHECK (native_quantity > 0),
    native_unit_code VARCHAR(32) NOT NULL,
    stock_movement_id UUID NULL REFERENCES inventory.stock_movements(stock_movement_id) ON DELETE SET NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_production_consumptions_batch
    ON production.production_consumptions (production_batch_id);
