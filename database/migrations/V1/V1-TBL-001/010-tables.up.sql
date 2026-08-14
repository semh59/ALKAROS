CREATE SCHEMA IF NOT EXISTS table_mgmt;

CREATE TABLE IF NOT EXISTS table_mgmt.zones (
    zone_id    UUID         NOT NULL,
    code       VARCHAR(50)  NOT NULL,
    name       VARCHAR(200) NOT NULL,
    sort_order INT          NOT NULL DEFAULT 0,
    active     BOOLEAN      NOT NULL DEFAULT TRUE,
    PRIMARY KEY (zone_id),
    UNIQUE (code)
);

CREATE TABLE IF NOT EXISTS table_mgmt.tables (
    table_id         UUID        NOT NULL,
    zone_id          UUID        NULL,
    table_number     TEXT        NOT NULL,
    capacity         INT         NOT NULL DEFAULT 0,
    active           BOOLEAN     NOT NULL DEFAULT TRUE,
    current_status   TEXT        NOT NULL CHECK (current_status IN ('Available', 'Occupied', 'Reserved', 'Cleaning', 'OutOfService')),
    current_order_id UUID        NULL,
    current_bill_id  UUID        NULL,
    row_version      BIGINT      NOT NULL DEFAULT 1,
    PRIMARY KEY (table_id),
    CONSTRAINT fk_tables_zone FOREIGN KEY (zone_id) REFERENCES table_mgmt.zones (zone_id),
    CONSTRAINT ux_tables_zone_number UNIQUE NULLS NOT DISTINCT (zone_id, table_number)
);

CREATE INDEX IF NOT EXISTS ix_tables_zone ON table_mgmt.tables (zone_id);