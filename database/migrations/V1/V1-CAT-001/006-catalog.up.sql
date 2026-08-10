CREATE SCHEMA IF NOT EXISTS catalog;

CREATE TABLE IF NOT EXISTS catalog.categories (
    category_id         UUID        NOT NULL,
    parent_category_id  UUID        NULL,
    code                VARCHAR(50) NOT NULL,
    name                VARCHAR(200) NOT NULL,
    sort_order          INT         NOT NULL DEFAULT 0,
    active              BOOLEAN     NOT NULL DEFAULT TRUE,
    PRIMARY KEY (category_id),
    UNIQUE (code),
    CONSTRAINT fk_categories_parent FOREIGN KEY (parent_category_id) REFERENCES catalog.categories (category_id)
);

CREATE INDEX IF NOT EXISTS ix_categories_parent ON catalog.categories (parent_category_id);

CREATE TABLE IF NOT EXISTS catalog.tax_profiles (
    tax_profile_id  UUID          NOT NULL,
    code            VARCHAR(50)   NOT NULL,
    name            VARCHAR(200)  NOT NULL,
    vat_rate        NUMERIC(5,2)  NOT NULL,
    active          BOOLEAN       NOT NULL DEFAULT TRUE,
    PRIMARY KEY (tax_profile_id),
    UNIQUE (code)
);

CREATE TABLE IF NOT EXISTS catalog.modifier_groups (
    modifier_group_id   UUID        NOT NULL,
    code                VARCHAR(50) NOT NULL,
    name                VARCHAR(200) NOT NULL,
    selection_type      SMALLINT    NOT NULL CHECK (selection_type IN (1,2)),
    min_selections      SMALLINT    NOT NULL DEFAULT 0 CHECK (min_selections >= 0),
    max_selections      SMALLINT    NOT NULL DEFAULT 1 CHECK (max_selections >= min_selections),
    active              BOOLEAN     NOT NULL DEFAULT TRUE,
    PRIMARY KEY (modifier_group_id),
    UNIQUE (code)
);

CREATE TABLE IF NOT EXISTS catalog.products (
    product_id            UUID            NOT NULL,
    sku                   VARCHAR(100)    NOT NULL,
    name                  VARCHAR(300)    NOT NULL,
    description           TEXT            NULL,
    category_id           UUID            NULL,
    tax_profile_id        UUID            NULL,
    product_type          SMALLINT        NOT NULL CHECK (product_type IN (1,2,3,4,5)),
    stock_mode            SMALLINT        NOT NULL CHECK (stock_mode IN (1,2,3,4)),
    active                BOOLEAN         NOT NULL DEFAULT TRUE,
    printer_route_policy  VARCHAR(200)    NULL,
    display_order         INT             NOT NULL DEFAULT 0,
    current_price         NUMERIC(18,2)   NULL,
    PRIMARY KEY (product_id),
    UNIQUE (sku),
    CONSTRAINT fk_products_category FOREIGN KEY (category_id) REFERENCES catalog.categories (category_id),
    CONSTRAINT fk_products_tax_profile FOREIGN KEY (tax_profile_id) REFERENCES catalog.tax_profiles (tax_profile_id)
);

CREATE INDEX IF NOT EXISTS ix_products_category ON catalog.products (category_id);
CREATE INDEX IF NOT EXISTS ix_products_active ON catalog.products (active) WHERE active;

CREATE TABLE IF NOT EXISTS catalog.modifiers (
    modifier_id         UUID            NOT NULL,
    modifier_group_id   UUID            NOT NULL,
    product_id          UUID            NULL,
    code                VARCHAR(50)     NOT NULL,
    name                VARCHAR(200)    NOT NULL,
    price_delta         NUMERIC(18,2)   NOT NULL DEFAULT 0,
    active              BOOLEAN         NOT NULL DEFAULT TRUE,
    PRIMARY KEY (modifier_id),
    UNIQUE (code),
    CONSTRAINT fk_modifiers_group FOREIGN KEY (modifier_group_id) REFERENCES catalog.modifier_groups (modifier_group_id) ON DELETE CASCADE,
    CONSTRAINT fk_modifiers_product FOREIGN KEY (product_id) REFERENCES catalog.products (product_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_modifiers_group ON catalog.modifiers (modifier_group_id);
CREATE INDEX IF NOT EXISTS ix_modifiers_product ON catalog.modifiers (product_id);

CREATE TABLE IF NOT EXISTS catalog.product_modifier_groups (
    product_modifier_group_id   UUID    NOT NULL,
    product_id                  UUID    NOT NULL,
    modifier_group_id           UUID    NOT NULL,
    PRIMARY KEY (product_modifier_group_id),
    UNIQUE (product_id, modifier_group_id),
    CONSTRAINT fk_pmg_product FOREIGN KEY (product_id) REFERENCES catalog.products (product_id) ON DELETE CASCADE,
    CONSTRAINT fk_pmg_modifier_group FOREIGN KEY (modifier_group_id) REFERENCES catalog.modifier_groups (modifier_group_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_pmg_modifier_group ON catalog.product_modifier_groups (modifier_group_id);
