CREATE TABLE table_mgmt.zone_floor_plans (
    zone_id       UUID   NOT NULL,
    canvas_width  INT    NOT NULL,
    canvas_height INT    NOT NULL,
    row_version   BIGINT NOT NULL DEFAULT 1,
    PRIMARY KEY (zone_id),
    CONSTRAINT fk_zone_floor_plans_zone
        FOREIGN KEY (zone_id) REFERENCES table_mgmt.zones (zone_id) ON DELETE CASCADE,
    CONSTRAINT ck_zone_floor_plans_canvas_width
        CHECK (canvas_width BETWEEN 320 AND 4096),
    CONSTRAINT ck_zone_floor_plans_canvas_height
        CHECK (canvas_height BETWEEN 320 AND 4096),
    CONSTRAINT ck_zone_floor_plans_row_version
        CHECK (row_version > 0)
);

CREATE TABLE table_mgmt.table_layouts (
    table_id        UUID        NOT NULL,
    zone_id         UUID        NOT NULL,
    x               INT         NOT NULL,
    y               INT         NOT NULL,
    width           INT         NOT NULL,
    height          INT         NOT NULL,
    shape           VARCHAR(20) NOT NULL,
    rotation_degrees INT        NOT NULL,
    row_version     BIGINT      NOT NULL DEFAULT 1,
    PRIMARY KEY (table_id),
    CONSTRAINT fk_table_layouts_table
        FOREIGN KEY (table_id) REFERENCES table_mgmt.tables (table_id) ON DELETE CASCADE,
    CONSTRAINT fk_table_layouts_zone
        FOREIGN KEY (zone_id) REFERENCES table_mgmt.zones (zone_id) ON DELETE CASCADE,
    CONSTRAINT ck_table_layouts_position CHECK (x >= 0 AND y >= 0),
    CONSTRAINT ck_table_layouts_size CHECK (width BETWEEN 48 AND 640 AND height BETWEEN 48 AND 640),
    CONSTRAINT ck_table_layouts_shape CHECK (shape IN ('Rectangle', 'Round', 'Square')),
    CONSTRAINT ck_table_layouts_rotation CHECK (rotation_degrees IN (0, 90, 180, 270)),
    CONSTRAINT ck_table_layouts_square CHECK (shape <> 'Square' OR width = height),
    CONSTRAINT ck_table_layouts_row_version CHECK (row_version > 0)
);

CREATE INDEX ix_table_layouts_zone ON table_mgmt.table_layouts (zone_id);

CREATE TABLE table_mgmt.table_seats (
    seat_id      UUID        NOT NULL,
    table_id     UUID        NOT NULL,
    seat_number  INT         NOT NULL,
    label        VARCHAR(50) NOT NULL,
    x            INT         NOT NULL,
    y            INT         NOT NULL,
    row_version  BIGINT      NOT NULL DEFAULT 1,
    PRIMARY KEY (seat_id),
    CONSTRAINT fk_table_seats_table
        FOREIGN KEY (table_id) REFERENCES table_mgmt.tables (table_id) ON DELETE CASCADE,
    CONSTRAINT ux_table_seats_number UNIQUE (table_id, seat_number) DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT ck_table_seats_number CHECK (seat_number > 0),
    CONSTRAINT ck_table_seats_label CHECK (length(btrim(label)) BETWEEN 1 AND 50),
    CONSTRAINT ck_table_seats_position CHECK (x >= 0 AND y >= 0),
    CONSTRAINT ck_table_seats_row_version CHECK (row_version > 0)
);

CREATE INDEX ix_table_seats_table ON table_mgmt.table_seats (table_id, seat_number);
