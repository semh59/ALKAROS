-- Monitoring: one row per distinct error (by fingerprint) with a running count, and one occurrence row per
-- written batch. Detail text has already passed the telemetry privacy guard; nothing here holds personal data.
CREATE SCHEMA IF NOT EXISTS observability;

CREATE TABLE IF NOT EXISTS observability.error_events (
    fingerprint          TEXT          NOT NULL,
    layer                TEXT          NOT NULL,
    severity             TEXT          NOT NULL,
    category             TEXT          NOT NULL,
    detail               TEXT          NULL,
    first_seen_at        TIMESTAMPTZ   NOT NULL,
    last_seen_at         TIMESTAMPTZ   NOT NULL,
    occurrence_count     BIGINT        NOT NULL,
    status               TEXT          NOT NULL DEFAULT 'Open',
    acknowledged_by      UUID          NULL,
    acknowledged_at      TIMESTAMPTZ   NULL,
    resolved_at          TIMESTAMPTZ   NULL,
    last_trace_id        TEXT          NULL,
    last_terminal_label  TEXT          NULL,
    synthetic            BOOLEAN       NOT NULL DEFAULT FALSE,
    alert_id             UUID          NULL,
    PRIMARY KEY (fingerprint),
    CONSTRAINT ck_error_events_layer CHECK (layer IN
        ('Database', 'Api', 'Background', 'Printer', 'Payment', 'Kitchen', 'Integration', 'Browser', 'Security', 'Backup')),
    CONSTRAINT ck_error_events_severity CHECK (severity IN ('Info', 'Warning', 'Error', 'Critical')),
    CONSTRAINT ck_error_events_status CHECK (status IN ('Open', 'Acknowledged', 'Resolved')),
    CONSTRAINT ck_error_events_count_positive CHECK (occurrence_count > 0),
    CONSTRAINT ck_error_events_detail_length CHECK (detail IS NULL OR length(detail) <= 200)
);

CREATE INDEX IF NOT EXISTS ix_error_events_status_last_seen ON observability.error_events (status, last_seen_at DESC);
CREATE INDEX IF NOT EXISTS ix_error_events_last_seen ON observability.error_events (last_seen_at);

CREATE TABLE IF NOT EXISTS observability.error_occurrences (
    occurrence_id  BIGSERIAL     NOT NULL,
    fingerprint    TEXT          NOT NULL,
    occurred_at    TIMESTAMPTZ   NOT NULL,
    severity       TEXT          NOT NULL,
    occurrences    INTEGER       NOT NULL,
    trace_id       TEXT          NULL,
    PRIMARY KEY (occurrence_id),
    CONSTRAINT fk_error_occurrences_event FOREIGN KEY (fingerprint) REFERENCES observability.error_events (fingerprint) ON DELETE CASCADE,
    CONSTRAINT ck_error_occurrences_count_positive CHECK (occurrences > 0)
);

CREATE INDEX IF NOT EXISTS ix_error_occurrences_fingerprint ON observability.error_occurrences (fingerprint, occurred_at DESC);
CREATE INDEX IF NOT EXISTS ix_error_occurrences_occurred_at ON observability.error_occurrences (occurred_at);

-- Monitoring's own health counters, e.g. events dropped because the in-memory queue was full.
CREATE TABLE IF NOT EXISTS observability.error_event_counters (
    name   TEXT    NOT NULL,
    value  BIGINT  NOT NULL DEFAULT 0,
    PRIMARY KEY (name)
);
