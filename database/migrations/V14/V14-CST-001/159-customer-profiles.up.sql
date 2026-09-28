CREATE SCHEMA IF NOT EXISTS customer_data;

CREATE TABLE IF NOT EXISTS customer_data.profiles (
    customer_id     UUID          NOT NULL PRIMARY KEY,
    envelope_bytes  BYTEA         NOT NULL,
    created_at      TIMESTAMPTZ   NOT NULL,
    anonymized      BOOLEAN       NOT NULL DEFAULT FALSE,
    row_version     INTEGER       NOT NULL DEFAULT 1
);
