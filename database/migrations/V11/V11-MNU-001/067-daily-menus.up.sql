CREATE SCHEMA IF NOT EXISTS menu;
CREATE SCHEMA IF NOT EXISTS recipe;

CREATE TABLE IF NOT EXISTS menu.daily_menus (
    daily_menu_id   UUID PRIMARY KEY,
    business_date   DATE NOT NULL UNIQUE,
    status          VARCHAR(32) NOT NULL DEFAULT 'Draft',
    opened_at       TIMESTAMPTZ NULL,
    closed_at       TIMESTAMPTZ NULL,
    closed_by       UUID NULL,
    note            TEXT NULL,
    row_version     INT NOT NULL DEFAULT 1,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_daily_menus_business_date ON menu.daily_menus(business_date);
CREATE INDEX IF NOT EXISTS idx_daily_menus_status ON menu.daily_menus(status);

CREATE TABLE IF NOT EXISTS menu.daily_menu_items (
    daily_menu_item_id      UUID PRIMARY KEY,
    daily_menu_id           UUID NOT NULL REFERENCES menu.daily_menus(daily_menu_id) ON DELETE CASCADE,
    product_id              UUID NOT NULL REFERENCES catalog.products(product_id) ON DELETE RESTRICT,
    product_name_snapshot   VARCHAR(300) NOT NULL,
    recipe_version_id       UUID NULL REFERENCES recipe.recipe_versions(id) ON DELETE SET NULL,
    price                   NUMERIC(18,2) NOT NULL CHECK (price >= 0),
    planned_portions        NUMERIC(10,3) NOT NULL DEFAULT 0 CHECK (planned_portions >= 0),
    prepared_portions       NUMERIC(10,3) NOT NULL DEFAULT 0 CHECK (prepared_portions >= 0),
    available_portions      NUMERIC(10,3) NOT NULL DEFAULT 0 CHECK (available_portions >= 0),
    reserved_portions       NUMERIC(10,3) NOT NULL DEFAULT 0 CHECK (reserved_portions >= 0),
    consumed_portions       NUMERIC(10,3) NOT NULL DEFAULT 0 CHECK (consumed_portions >= 0),
    waste_portions          NUMERIC(10,3) NOT NULL DEFAULT 0 CHECK (waste_portions >= 0),
    out_of_stock            BOOLEAN NOT NULL DEFAULT false,
    printer_route_policy    VARCHAR(200) NULL,
    active                  BOOLEAN NOT NULL DEFAULT true,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_daily_menu_items UNIQUE (daily_menu_id, product_id)
);

CREATE INDEX IF NOT EXISTS idx_daily_menu_items_menu ON menu.daily_menu_items(daily_menu_id);
CREATE INDEX IF NOT EXISTS idx_daily_menu_items_product ON menu.daily_menu_items(product_id);

CREATE TABLE IF NOT EXISTS menu.daily_menu_item_history (
    daily_menu_item_history_id  UUID PRIMARY KEY,
    daily_menu_item_id          UUID NOT NULL REFERENCES menu.daily_menu_items(daily_menu_item_id) ON DELETE CASCADE,
    old_value                   JSONB NOT NULL,
    new_value                   JSONB NOT NULL,
    changed_by                  UUID NULL,
    changed_at                  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_daily_menu_item_history_item ON menu.daily_menu_item_history(daily_menu_item_id);
