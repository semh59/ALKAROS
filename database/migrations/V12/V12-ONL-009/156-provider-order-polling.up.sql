-- V12-ONL-009: per-platform order polling. The cursor is the platform's own "read up to here" marker; it only
-- advances after every polled event is in the shared inbox, so a failed or rate-limited poll never loses an order.
CREATE SCHEMA IF NOT EXISTS online_ordering;

CREATE TABLE IF NOT EXISTS online_ordering.provider_poll_state (
    provider              TEXT         NOT NULL,
    poll_cursor           TEXT         NULL,
    next_poll_at          TIMESTAMPTZ  NOT NULL DEFAULT now(),
    consecutive_failures  INT          NOT NULL DEFAULT 0,
    failing_since         TIMESTAMPTZ  NULL,
    last_error            TEXT         NULL,
    last_success_at       TIMESTAMPTZ  NULL,
    CONSTRAINT pk_provider_poll_state PRIMARY KEY (provider),
    CONSTRAINT ck_provider_poll_state_provider CHECK (provider ~ '^[a-z][a-z0-9-]{1,31}$'),
    CONSTRAINT ck_provider_poll_state_cursor CHECK (poll_cursor IS NULL OR length(poll_cursor) <= 512),
    CONSTRAINT ck_provider_poll_state_failures CHECK (consecutive_failures >= 0),
    -- A failure streak always knows when it started, and only a streak has a start.
    CONSTRAINT ck_provider_poll_state_streak CHECK ((consecutive_failures = 0) = (failing_since IS NULL))
);
