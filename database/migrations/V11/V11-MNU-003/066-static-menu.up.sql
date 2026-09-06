CREATE SCHEMA IF NOT EXISTS menu;

CREATE TABLE IF NOT EXISTS menu.menus (
    menu_id     UUID PRIMARY KEY,
    code        VARCHAR(64) NOT NULL UNIQUE,
    name        VARCHAR(255) NOT NULL,
    active      BOOLEAN NOT NULL DEFAULT true,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_menus_code ON menu.menus(code);
CREATE INDEX IF NOT EXISTS idx_menus_active ON menu.menus(active);

CREATE TABLE IF NOT EXISTS menu.menu_items (
    menu_item_id    UUID PRIMARY KEY,
    menu_id         UUID NOT NULL REFERENCES menu.menus(menu_id) ON DELETE CASCADE,
    product_id      UUID NOT NULL REFERENCES catalog.products(product_id) ON DELETE RESTRICT,
    display_order   INT NOT NULL DEFAULT 0,
    active          BOOLEAN NOT NULL DEFAULT true,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_menu_items_menu_product UNIQUE (menu_id, product_id)
);

CREATE INDEX IF NOT EXISTS idx_menu_items_menu_id ON menu.menu_items(menu_id);
CREATE INDEX IF NOT EXISTS idx_menu_items_product_id ON menu.menu_items(product_id);
CREATE INDEX IF NOT EXISTS idx_menu_items_menu_order ON menu.menu_items(menu_id, display_order);
