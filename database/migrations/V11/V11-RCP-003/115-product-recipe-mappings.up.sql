-- Migration 115: Product-to-recipe mapping (V11-RCP-003)
-- Links a catalog product to the recipe that explains what it consumes.
-- One recipe per product (unlike inventory.product_stock_mappings, which
-- allows several stock items per product) - product_id alone is the key.
-- No FK to catalog.products, matching product_stock_mappings' own
-- deliberate lack of a cross-schema FK (Inventory reads Catalog ids by
-- value, never joins across the schema boundary at the constraint level).

CREATE TABLE IF NOT EXISTS recipe.product_recipe_mappings (
    product_id UUID PRIMARY KEY,
    recipe_id UUID NOT NULL REFERENCES recipe.recipes(id) ON DELETE RESTRICT,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    notes VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_product_recipe_mappings_recipe
    ON recipe.product_recipe_mappings (recipe_id);
