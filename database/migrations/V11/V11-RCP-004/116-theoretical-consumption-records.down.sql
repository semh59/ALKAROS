DROP TRIGGER IF EXISTS trg_theoretical_consumption_immutable ON recipe.theoretical_consumption_records;
DROP FUNCTION IF EXISTS recipe.prevent_theoretical_consumption_mutation();
DROP TABLE IF EXISTS recipe.theoretical_consumption_records;
