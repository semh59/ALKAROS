CREATE SCHEMA IF NOT EXISTS production;

CREATE TABLE IF NOT EXISTS production.production_batches (
    production_batch_id UUID PRIMARY KEY,
    batch_number VARCHAR(64) NOT NULL UNIQUE,
    recipe_version_id UUID NOT NULL REFERENCES recipe.recipe_versions(id) ON DELETE RESTRICT,
    daily_menu_item_id UUID NULL REFERENCES menu.daily_menu_items(daily_menu_item_id) ON DELETE SET NULL,
    status VARCHAR(32) NOT NULL CHECK (status IN ('Planned', 'InProgress', 'Completed', 'Cancelled')),
    planned_quantity NUMERIC(14, 4) NOT NULL CHECK (planned_quantity > 0),
    actual_quantity NUMERIC(14, 4) NOT NULL DEFAULT 0 CHECK (actual_quantity >= 0),
    portion_unit_code VARCHAR(32) NOT NULL DEFAULT 'portion',
    destination_location_id UUID NULL REFERENCES inventory.stock_locations(id) ON DELETE SET NULL,
    started_at TIMESTAMPTZ NULL,
    completed_at TIMESTAMPTZ NULL,
    produced_at TIMESTAMPTZ NULL,
    cancelled_at TIMESTAMPTZ NULL,
    cancellation_reason TEXT NULL,
    notes TEXT NULL,
    created_by UUID NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    row_version INTEGER NOT NULL DEFAULT 1
);

CREATE INDEX IF NOT EXISTS idx_production_batches_recipe_version
    ON production.production_batches (recipe_version_id);

CREATE INDEX IF NOT EXISTS idx_production_batches_daily_menu_item
    ON production.production_batches (daily_menu_item_id);

CREATE INDEX IF NOT EXISTS idx_production_batches_status
    ON production.production_batches (status);

CREATE INDEX IF NOT EXISTS idx_production_batches_created_at
    ON production.production_batches (created_at);
