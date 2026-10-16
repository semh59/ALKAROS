-- Performance budgets (an explicit p95 limit per metric and action) and the first time each build was seen,
-- so a slowdown after a new release can be told from an ordinary one.
CREATE SCHEMA IF NOT EXISTS observability;

CREATE TABLE IF NOT EXISTS observability.perf_budgets (
    metric   TEXT              NOT NULL,
    name     TEXT              NOT NULL,
    p95_ms   DOUBLE PRECISION  NOT NULL,
    PRIMARY KEY (metric, name),
    CONSTRAINT ck_perf_budgets_p95 CHECK (p95_ms > 0)
);

CREATE TABLE IF NOT EXISTS observability.perf_releases (
    version        TEXT         NOT NULL,
    first_seen_at  TIMESTAMPTZ  NOT NULL,
    PRIMARY KEY (version)
);
