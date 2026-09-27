-- V12-ONL-011: whether the restaurant takes orders on each online platform, as a manager asked for it. A request is kept
-- until the platform has been told (delivered_at); a timed closure ends at closed_until, when the platform is opened again
-- (by itself where it supports a closing time, otherwise by ALKAROS).
CREATE SCHEMA IF NOT EXISTS online_ordering;

CREATE TABLE IF NOT EXISTS online_ordering.platform_store_status (
    provider           TEXT          NOT NULL,
    desired_state      TEXT          NOT NULL,
    closed_until       TIMESTAMPTZ   NULL,
    close_reason       TEXT          NULL,
    requested_by       UUID          NULL,
    requested_at       TIMESTAMPTZ   NOT NULL,
    delivered_at       TIMESTAMPTZ   NULL,
    delivery_attempts  INT           NOT NULL DEFAULT 0,
    next_attempt_at    TIMESTAMPTZ   NULL,
    last_error         TEXT          NULL,
    CONSTRAINT pk_platform_store_status PRIMARY KEY (provider),
    CONSTRAINT ck_platform_store_status_provider CHECK (provider ~ '^[a-z][a-z0-9-]{1,31}$'),
    CONSTRAINT ck_platform_store_status_state CHECK (desired_state IN ('Open', 'ClosedToday', 'ClosedUntil')),
    -- An open restaurant has no closing time or reason; a closed one always has both. Written as two
    -- mutually exclusive branches (not a single boolean equality) so that "closed but missing one of the
    -- two" is rejected too, not just "closed but missing both" - equality between two booleans only forces
    -- them to agree on TRUE/FALSE, it does not force closed_until and close_reason to agree WITH EACH OTHER.
    CONSTRAINT ck_platform_store_status_closure CHECK (
        (desired_state = 'Open' AND closed_until IS NULL AND close_reason IS NULL)
        OR (desired_state <> 'Open' AND closed_until IS NOT NULL AND close_reason IS NOT NULL)),
    CONSTRAINT ck_platform_store_status_reason CHECK (close_reason IS NULL OR close_reason IN ('Busy', 'Closed')),
    CONSTRAINT ck_platform_store_status_attempts CHECK (delivery_attempts >= 0)
);
