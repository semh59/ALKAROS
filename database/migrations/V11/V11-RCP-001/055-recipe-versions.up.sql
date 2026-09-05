-- Migration 055: Recipe and RecipeVersion lifecycle (V11-RCP-001)
-- Requires schema recipe (created in migration 054)

CREATE TABLE IF NOT EXISTS recipe.recipes (
    id UUID PRIMARY KEY,
    code VARCHAR(64) NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    row_version INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT uq_recipes_code UNIQUE (code)
);

CREATE TABLE IF NOT EXISTS recipe.recipe_versions (
    id UUID PRIMARY KEY,
    recipe_id UUID NOT NULL REFERENCES recipe.recipes(id) ON DELETE RESTRICT,
    version_number INTEGER NOT NULL CHECK (version_number > 0),
    status VARCHAR(32) NOT NULL CHECK (status IN ('Draft', 'Active', 'Archived', 'Deprecated')),
    effective_from TIMESTAMPTZ,
    effective_to TIMESTAMPTZ,
    yield_quantity NUMERIC(14, 4) NOT NULL CHECK (yield_quantity > 0),
    yield_unit_code VARCHAR(32) NOT NULL,
    preparation_minutes INTEGER NOT NULL DEFAULT 0 CHECK (preparation_minutes >= 0),
    instructions TEXT,
    is_locked BOOLEAN NOT NULL DEFAULT FALSE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    activated_at TIMESTAMPTZ,
    row_version INTEGER NOT NULL DEFAULT 1,
    CONSTRAINT uq_recipe_version_number UNIQUE (recipe_id, version_number)
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_recipe_single_active
    ON recipe.recipe_versions (recipe_id)
    WHERE status = 'Active';

CREATE INDEX IF NOT EXISTS idx_recipe_versions_recipe_id
    ON recipe.recipe_versions (recipe_id);

CREATE TABLE IF NOT EXISTS recipe.recipe_ingredients (
    id UUID PRIMARY KEY,
    recipe_version_id UUID NOT NULL REFERENCES recipe.recipe_versions(id) ON DELETE RESTRICT,
    ingredient_item_id UUID NOT NULL,
    quantity NUMERIC(14, 4) NOT NULL CHECK (quantity > 0),
    unit_code VARCHAR(32) NOT NULL,
    loss_percentage NUMERIC(5, 2) NOT NULL DEFAULT 0.00 CHECK (loss_percentage >= 0 AND loss_percentage < 100.00),
    sort_order INTEGER NOT NULL DEFAULT 0,
    notes VARCHAR(255),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_recipe_ingredient_item UNIQUE (recipe_version_id, ingredient_item_id)
);

CREATE INDEX IF NOT EXISTS idx_recipe_ingredients_version_id
    ON recipe.recipe_ingredients (recipe_version_id);
