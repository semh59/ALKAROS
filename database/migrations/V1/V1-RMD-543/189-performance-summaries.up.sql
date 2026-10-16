-- Per-minute performance summaries: one row per (minute, metric, name) with a fixed-bucket histogram, so
-- percentiles are computed from summed buckets, never from averaged percentiles. Values are numbers only.
CREATE SCHEMA IF NOT EXISTS observability;

CREATE TABLE IF NOT EXISTS observability.perf_minute_summaries (
    minute        TIMESTAMPTZ       NOT NULL,
    metric        TEXT              NOT NULL,
    name          TEXT              NOT NULL,
    sample_count  BIGINT            NOT NULL,
    error_count   BIGINT            NOT NULL DEFAULT 0,
    sum_value     DOUBLE PRECISION  NOT NULL,
    max_value     DOUBLE PRECISION  NOT NULL,
    buckets       BIGINT[]          NOT NULL,
    PRIMARY KEY (minute, metric, name),
    CONSTRAINT ck_perf_minute_summaries_counts CHECK (sample_count >= 0 AND error_count >= 0 AND error_count <= sample_count)
);

CREATE INDEX IF NOT EXISTS ix_perf_minute_summaries_metric_name ON observability.perf_minute_summaries (metric, name, minute);
