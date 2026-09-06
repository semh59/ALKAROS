-- Migration 058 Down: Drop recipe tables (V11-RCP-001)

DROP TABLE IF EXISTS recipe.recipe_ingredients;
DROP TABLE IF EXISTS recipe.recipe_versions;
DROP TABLE IF EXISTS recipe.recipes;
