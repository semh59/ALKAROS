-- The trace id of the first API request a click started, so the user journey can show the click as its first step.
-- Nullable: a click that started no request, or whose id was not valid, is stored without one.
ALTER TABLE observability.raw_ui_interactions ADD COLUMN IF NOT EXISTS trace_id TEXT NULL;

CREATE INDEX IF NOT EXISTS ix_raw_ui_interactions_trace ON observability.raw_ui_interactions (trace_id) WHERE trace_id IS NOT NULL;
