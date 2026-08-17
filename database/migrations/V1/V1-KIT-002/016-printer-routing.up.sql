CREATE SCHEMA IF NOT EXISTS kitchen;

CREATE TABLE IF NOT EXISTS kitchen.printers (
    id          UUID         PRIMARY KEY,
    name        VARCHAR(100) NOT NULL UNIQUE,
    station_id  VARCHAR(64)  NOT NULL,
    ip_address  VARCHAR(64)  NULL,
    port        INT          NULL,
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ  NULL
);

CREATE INDEX IF NOT EXISTS ix_kitchen_printers_station_id
    ON kitchen.printers (station_id);

CREATE TABLE IF NOT EXISTS kitchen.printer_routes (
    id           UUID        PRIMARY KEY,
    route_level  VARCHAR(32) NOT NULL CHECK (route_level IN ('Item', 'Product', 'DailySpecial', 'Category', 'Default')),
    item_id      UUID        NULL,
    product_id   UUID        NULL,
    category_id  UUID        NULL,
    special_date DATE        NULL,
    printer_id   UUID        NOT NULL REFERENCES kitchen.printers(id) ON DELETE RESTRICT,
    is_active    BOOLEAN     NOT NULL DEFAULT TRUE,
    created_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at   TIMESTAMPTZ NULL,
    CONSTRAINT chk_route_level_payload CHECK (
        (route_level = 'Item' AND item_id IS NOT NULL AND product_id IS NULL AND category_id IS NULL AND special_date IS NULL) OR
        (route_level = 'Product' AND product_id IS NOT NULL AND item_id IS NULL AND category_id IS NULL AND special_date IS NULL) OR
        (route_level = 'DailySpecial' AND special_date IS NOT NULL AND category_id IS NOT NULL AND item_id IS NULL AND product_id IS NULL) OR
        (route_level = 'Category' AND category_id IS NOT NULL AND item_id IS NULL AND product_id IS NULL AND special_date IS NULL) OR
        (route_level = 'Default' AND item_id IS NULL AND product_id IS NULL AND category_id IS NULL AND special_date IS NULL)
    )
);

CREATE INDEX IF NOT EXISTS ix_printer_routes_printer_id
    ON kitchen.printer_routes (printer_id);

-- Ambiguity prevention: no duplicate active routes at the same specificity level for the same target
CREATE UNIQUE INDEX IF NOT EXISTS ix_printer_routes_unique_default
    ON kitchen.printer_routes (route_level)
    WHERE route_level = 'Default' AND is_active = TRUE;

CREATE UNIQUE INDEX IF NOT EXISTS ix_printer_routes_unique_category
    ON kitchen.printer_routes (category_id)
    WHERE route_level = 'Category' AND is_active = TRUE;

CREATE UNIQUE INDEX IF NOT EXISTS ix_printer_routes_unique_product
    ON kitchen.printer_routes (product_id)
    WHERE route_level = 'Product' AND is_active = TRUE;

CREATE UNIQUE INDEX IF NOT EXISTS ix_printer_routes_unique_item
    ON kitchen.printer_routes (item_id)
    WHERE route_level = 'Item' AND is_active = TRUE;

CREATE UNIQUE INDEX IF NOT EXISTS ix_printer_routes_unique_daily_special
    ON kitchen.printer_routes (special_date, category_id)
    WHERE route_level = 'DailySpecial' AND is_active = TRUE;
