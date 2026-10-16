DROP INDEX IF EXISTS observability.ix_raw_ui_interactions_trace;
ALTER TABLE observability.raw_ui_interactions DROP COLUMN IF EXISTS trace_id;
