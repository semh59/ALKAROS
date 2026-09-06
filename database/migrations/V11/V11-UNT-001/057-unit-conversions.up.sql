-- V11-UNT-001: Unit conversions schema and table
CREATE SCHEMA IF NOT EXISTS recipe;

CREATE TABLE IF NOT EXISTS recipe.unit_conversions (
    unit_conversion_id UUID PRIMARY KEY,
    from_unit_code VARCHAR(32) NOT NULL,
    to_unit_code VARCHAR(32) NOT NULL,
    factor NUMERIC(18, 9) NOT NULL,
    active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT clock_timestamp(),
    CONSTRAINT uq_unit_conversions_from_to UNIQUE (from_unit_code, to_unit_code),
    CONSTRAINT ck_unit_conversions_factor_positive CHECK (factor > 0)
);

CREATE INDEX IF NOT EXISTS ix_unit_conversions_active
    ON recipe.unit_conversions (from_unit_code, to_unit_code)
    WHERE active = true;
