-- Migration 116: Theoretical consumption records (V11-RCP-004)
-- Append-only shadow ledger: what a recipe SAYS an order item should have
-- consumed, computed from the active RecipeVersion at Accept time. Never
-- touches inventory.stock_balances — this is informational only, feeding
-- the future actual-vs-theoretical variance report (V11-RPT-003).

CREATE TABLE IF NOT EXISTS recipe.theoretical_consumption_records (
    id UUID PRIMARY KEY,
    order_item_id UUID NOT NULL,
    product_id UUID NOT NULL,
    recipe_id UUID NOT NULL REFERENCES recipe.recipes(id) ON DELETE RESTRICT,
    recipe_version_id UUID NOT NULL REFERENCES recipe.recipe_versions(id) ON DELETE RESTRICT,
    stock_item_id UUID NOT NULL,
    quantity NUMERIC(14, 4) NOT NULL CHECK (quantity > 0),
    unit_code VARCHAR(32) NOT NULL,
    recorded_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_theoretical_consumption_stock_item_recorded_at
    ON recipe.theoretical_consumption_records (stock_item_id, recorded_at);

CREATE INDEX IF NOT EXISTS idx_theoretical_consumption_order_item
    ON recipe.theoretical_consumption_records (order_item_id);

-- Immutability enforcement trigger, same pattern as inventory.stock_movements.
CREATE OR REPLACE FUNCTION recipe.prevent_theoretical_consumption_mutation()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'Theoretical consumption records are immutable and cannot be updated or deleted.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_theoretical_consumption_immutable ON recipe.theoretical_consumption_records;
CREATE TRIGGER trg_theoretical_consumption_immutable
BEFORE UPDATE OR DELETE ON recipe.theoretical_consumption_records
FOR EACH ROW EXECUTE FUNCTION recipe.prevent_theoretical_consumption_mutation();
