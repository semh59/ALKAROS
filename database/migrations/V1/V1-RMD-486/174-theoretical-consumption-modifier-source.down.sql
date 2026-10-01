-- Rolling back removes the extra-sourced rows (the table is append-only, so its guard trigger is lifted for this one statement).
ALTER TABLE recipe.theoretical_consumption_records DISABLE TRIGGER trg_theoretical_consumption_immutable;
DELETE FROM recipe.theoretical_consumption_records WHERE modifier_id IS NOT NULL;
ALTER TABLE recipe.theoretical_consumption_records ENABLE TRIGGER trg_theoretical_consumption_immutable;

ALTER TABLE recipe.theoretical_consumption_records DROP CONSTRAINT IF EXISTS ck_theoretical_consumption_source;
ALTER TABLE recipe.theoretical_consumption_records DROP COLUMN IF EXISTS modifier_id;
ALTER TABLE recipe.theoretical_consumption_records ALTER COLUMN recipe_version_id SET NOT NULL;
ALTER TABLE recipe.theoretical_consumption_records ALTER COLUMN recipe_id SET NOT NULL;
