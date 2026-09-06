CREATE SCHEMA IF NOT EXISTS recipe;

CREATE TABLE IF NOT EXISTS recipe.recipe_cost_snapshots (
    snapshot_id             UUID PRIMARY KEY,
    recipe_version_id       UUID NOT NULL REFERENCES recipe.recipe_versions(id) ON DELETE CASCADE,
    cost_basis_date         DATE NOT NULL,
    calculated_cost         NUMERIC(18, 2) NOT NULL,
    currency                VARCHAR(3) NOT NULL DEFAULT 'TRY',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_recipe_cost_snapshots_version_date UNIQUE (recipe_version_id, cost_basis_date)
);

CREATE INDEX IF NOT EXISTS idx_recipe_cost_snapshots_lookup
    ON recipe.recipe_cost_snapshots (recipe_version_id, cost_basis_date DESC);

CREATE TABLE IF NOT EXISTS recipe.recipe_cost_snapshot_items (
    snapshot_item_id        UUID PRIMARY KEY,
    snapshot_id             UUID NOT NULL REFERENCES recipe.recipe_cost_snapshots(snapshot_id) ON DELETE CASCADE,
    stock_item_id           UUID NOT NULL,
    raw_quantity            NUMERIC(18, 4) NOT NULL,
    waste_factor            NUMERIC(14, 4) NOT NULL DEFAULT 0,
    effective_native_quantity NUMERIC(18, 4) NOT NULL,
    native_unit_code        VARCHAR(32) NOT NULL,
    stock_quantity          NUMERIC(18, 4) NOT NULL,
    stock_unit_code         VARCHAR(32) NOT NULL,
    unit_cost               NUMERIC(18, 4) NOT NULL,
    line_cost               NUMERIC(18, 2) NOT NULL,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_cost_snapshot_items_snapshot
    ON recipe.recipe_cost_snapshot_items (snapshot_id);
