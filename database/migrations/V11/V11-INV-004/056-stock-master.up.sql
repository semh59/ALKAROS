-- Migration 056: Stock Item and Stock Location Master Data (V11-INV-004)
CREATE SCHEMA IF NOT EXISTS inventory;

CREATE TABLE IF NOT EXISTS inventory.stock_locations (
    id UUID PRIMARY KEY,
    code VARCHAR(64) NOT NULL,
    name VARCHAR(255) NOT NULL,
    location_type VARCHAR(32) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    row_version INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT uq_stock_locations_code UNIQUE (code)
);

CREATE INDEX IF NOT EXISTS idx_stock_locations_active
    ON inventory.stock_locations (is_active);

CREATE TABLE IF NOT EXISTS inventory.stock_items (
    id UUID PRIMARY KEY,
    code VARCHAR(64) NOT NULL,
    name VARCHAR(255) NOT NULL,
    item_type VARCHAR(32) NOT NULL,
    tracking_unit_code VARCHAR(32) NOT NULL,
    default_location_id UUID REFERENCES inventory.stock_locations(id) ON DELETE SET NULL,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    row_version INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT uq_stock_items_code UNIQUE (code)
);

CREATE INDEX IF NOT EXISTS idx_stock_items_active
    ON inventory.stock_items (is_active);

CREATE INDEX IF NOT EXISTS idx_stock_items_default_location
    ON inventory.stock_items (default_location_id);

CREATE TABLE IF NOT EXISTS inventory.product_stock_mappings (
    product_id UUID NOT NULL,
    stock_item_id UUID NOT NULL REFERENCES inventory.stock_items(id) ON DELETE RESTRICT,
    quantity_multiplier NUMERIC(14, 4) NOT NULL CHECK (quantity_multiplier > 0),
    notes VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT pk_product_stock_mappings PRIMARY KEY (product_id, stock_item_id)
);

CREATE INDEX IF NOT EXISTS idx_product_stock_mappings_stock_item
    ON inventory.product_stock_mappings (stock_item_id);
