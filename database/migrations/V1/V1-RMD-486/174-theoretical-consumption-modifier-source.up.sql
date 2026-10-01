-- A theoretical consumption record now comes either from a recipe version or from an order-line extra (modifier) that is
-- mapped to a stock item, so the actual-vs-theoretical report explains the stock an extra really takes.
ALTER TABLE recipe.theoretical_consumption_records ALTER COLUMN recipe_id DROP NOT NULL;
ALTER TABLE recipe.theoretical_consumption_records ALTER COLUMN recipe_version_id DROP NOT NULL;
ALTER TABLE recipe.theoretical_consumption_records ADD COLUMN IF NOT EXISTS modifier_id UUID NULL;

ALTER TABLE recipe.theoretical_consumption_records DROP CONSTRAINT IF EXISTS ck_theoretical_consumption_source;
ALTER TABLE recipe.theoretical_consumption_records ADD CONSTRAINT ck_theoretical_consumption_source CHECK (
    (recipe_id IS NOT NULL AND recipe_version_id IS NOT NULL AND modifier_id IS NULL)
    OR (recipe_id IS NULL AND recipe_version_id IS NULL AND modifier_id IS NOT NULL));
