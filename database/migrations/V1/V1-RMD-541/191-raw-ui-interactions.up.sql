-- Raw click-level interaction log. Unlogged (no write-ahead log, not crash safe) and prefixed raw_, so it is
-- left out of the database backup; rows older than the raw-event retention are purged by the monitoring job.
-- No visible text, accessible name or typed value is ever stored: only a fixed key chosen by the client catalog.
CREATE SCHEMA IF NOT EXISTS observability;

CREATE UNLOGGED TABLE IF NOT EXISTS observability.raw_ui_interactions (
    id                  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    occurred_at         TIMESTAMPTZ  NOT NULL,
    terminal_id         UUID         NOT NULL,
    role                TEXT         NOT NULL,
    key                 TEXT         NOT NULL,
    kind                TEXT         NOT NULL,
    duration_ms         INTEGER      NOT NULL,
    long_task_count     INTEGER      NOT NULL DEFAULT 0,
    layout_shift_milli  INTEGER      NOT NULL DEFAULT 0,
    outcome             TEXT         NOT NULL,
    CONSTRAINT ck_raw_ui_interactions_kind CHECK (kind IN ('click', 'navigate', 'submit')),
    CONSTRAINT ck_raw_ui_interactions_outcome CHECK (outcome IN ('ok', 'error', 'cancelled')),
    CONSTRAINT ck_raw_ui_interactions_duration CHECK (duration_ms >= 0)
);

CREATE INDEX IF NOT EXISTS ix_raw_ui_interactions_time ON observability.raw_ui_interactions (occurred_at);
CREATE INDEX IF NOT EXISTS ix_raw_ui_interactions_key ON observability.raw_ui_interactions (key, occurred_at);
