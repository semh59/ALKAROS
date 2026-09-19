-- Migration 136: Business identity logo image (V1-SET-008)
-- Singleton table: exactly one business-wide logo image, never per-terminal
-- (V1-SET-007's own business.name/business.accent_theme are the same
-- single-global-row model; no deployment in this app is multi-branch yet).
CREATE TABLE settings.business_logo (
    id            SMALLINT    NOT NULL DEFAULT 1 CHECK (id = 1),
    content       BYTEA       NOT NULL,
    content_type  VARCHAR(64) NOT NULL,
    updated_at    TIMESTAMPTZ NOT NULL,
    row_version   INTEGER     NOT NULL DEFAULT 1 CHECK (row_version > 0),
    PRIMARY KEY (id)
);
