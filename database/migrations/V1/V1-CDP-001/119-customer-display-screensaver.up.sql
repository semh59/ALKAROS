-- Migration 119: Customer Display Idle Screensaver Image (V1-CDP-001)
-- Singleton table: exactly one business-wide screensaver image, never
-- per-terminal (no deployment in this app is multi-branch yet).
CREATE TABLE customer_display.screensaver_images (
    id            SMALLINT    NOT NULL DEFAULT 1 CHECK (id = 1),
    content       BYTEA       NOT NULL,
    content_type  VARCHAR(64) NOT NULL,
    updated_at    TIMESTAMPTZ NOT NULL,
    row_version   INTEGER     NOT NULL DEFAULT 1 CHECK (row_version > 0),
    PRIMARY KEY (id)
);
