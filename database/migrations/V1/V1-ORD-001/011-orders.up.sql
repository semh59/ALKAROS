CREATE SCHEMA IF NOT EXISTS orders;

CREATE TABLE IF NOT EXISTS orders.orders (
    order_id             UUID          NOT NULL,
    source               TEXT          NOT NULL CHECK (source IN ('Cashier', 'Waiter', 'Qr', 'Online')),
    source_reference_id  UUID          NULL,
    source_external_id   TEXT          NULL,
    table_id             UUID          NULL,
    customer_id          UUID          NULL,
    status               TEXT          NOT NULL CHECK (status IN ('Draft', 'Submitted', 'PendingConfirmation', 'Accepted', 'Rejected', 'Preparing', 'Ready', 'Served', 'Completed', 'Cancelled')),
    confirmation_status  TEXT          NOT NULL CHECK (confirmation_status IN ('NotRequired', 'Pending', 'Accepted', 'Rejected')),
    order_number         VARCHAR(50)   NOT NULL,
    notes                TEXT          NULL,
    subtotal             NUMERIC(18,2) NOT NULL DEFAULT 0,
    discount_total       NUMERIC(18,2) NOT NULL DEFAULT 0,
    tax_total            NUMERIC(18,2) NOT NULL DEFAULT 0,
    total                NUMERIC(18,2) NOT NULL DEFAULT 0,
    currency_code        CHAR(3)       NOT NULL DEFAULT 'TRY',
    submitted_at         TIMESTAMPTZ   NULL,
    accepted_at          TIMESTAMPTZ   NULL,
    closed_at            TIMESTAMPTZ   NULL,
    cancelled_at         TIMESTAMPTZ   NULL,
    created_at           TIMESTAMPTZ   NOT NULL,
    updated_at           TIMESTAMPTZ   NOT NULL,
    row_version          BIGINT        NOT NULL DEFAULT 1,
    PRIMARY KEY (order_id),
    UNIQUE (order_number),
    CONSTRAINT fk_orders_table FOREIGN KEY (table_id) REFERENCES table_mgmt.tables (table_id)
);

CREATE INDEX IF NOT EXISTS ix_orders_table ON orders.orders (table_id);

CREATE TABLE IF NOT EXISTS orders.order_items (
    order_item_id               UUID          NOT NULL,
    order_id                    UUID          NOT NULL,
    product_id                  UUID          NOT NULL,
    product_name_snapshot       TEXT          NOT NULL,
    sku_snapshot                VARCHAR(100)  NULL,
    quantity                    NUMERIC(18,3) NOT NULL,
    unit_price                  NUMERIC(18,2) NOT NULL,
    discount_amount             NUMERIC(18,2) NOT NULL DEFAULT 0,
    tax_rate                    NUMERIC(5,2)  NOT NULL,
    tax_amount                  NUMERIC(18,2) NOT NULL DEFAULT 0,
    net_amount                  NUMERIC(18,2) NOT NULL,
    gross_amount                NUMERIC(18,2) NOT NULL,
    status                      TEXT          NOT NULL CHECK (status IN ('Draft', 'Active', 'Cancelled', 'Waste', 'Complimentary')),
    kitchen_state               TEXT          NOT NULL CHECK (kitchen_state IN ('NotSent', 'Sent', 'Preparing', 'Ready', 'Served', 'Cancelled')),
    portion_reservation_status  TEXT          NOT NULL CHECK (portion_reservation_status IN ('NotApplicable', 'NotReserved', 'Reserved', 'Released', 'Consumed', 'Waste')),
    notes                       TEXT          NULL,
    created_at                  TIMESTAMPTZ   NOT NULL,
    updated_at                  TIMESTAMPTZ   NOT NULL,
    row_version                 BIGINT        NOT NULL DEFAULT 1,
    PRIMARY KEY (order_item_id),
    CONSTRAINT fk_order_items_order FOREIGN KEY (order_id) REFERENCES orders.orders (order_id) ON DELETE CASCADE,
    CONSTRAINT fk_order_items_product FOREIGN KEY (product_id) REFERENCES catalog.products (product_id)
);

CREATE INDEX IF NOT EXISTS ix_order_items_order ON orders.order_items (order_id);
CREATE INDEX IF NOT EXISTS ix_order_items_product ON orders.order_items (product_id);

CREATE TABLE IF NOT EXISTS orders.order_item_modifiers (
    order_item_modifier_id  UUID          NOT NULL,
    order_item_id           UUID          NOT NULL,
    modifier_id             UUID          NOT NULL,
    modifier_name_snapshot  TEXT          NOT NULL,
    price_delta             NUMERIC(18,2) NOT NULL DEFAULT 0,
    quantity                NUMERIC(18,3) NOT NULL DEFAULT 1,
    PRIMARY KEY (order_item_modifier_id),
    CONSTRAINT fk_order_item_modifiers_item FOREIGN KEY (order_item_id) REFERENCES orders.order_items (order_item_id) ON DELETE CASCADE,
    CONSTRAINT fk_order_item_modifiers_modifier FOREIGN KEY (modifier_id) REFERENCES catalog.modifiers (modifier_id)
);

CREATE INDEX IF NOT EXISTS ix_order_item_modifiers_item ON orders.order_item_modifiers (order_item_id);
CREATE INDEX IF NOT EXISTS ix_order_item_modifiers_modifier ON orders.order_item_modifiers (modifier_id);

CREATE TABLE IF NOT EXISTS orders.order_status_history (
    order_status_history_id  UUID          NOT NULL,
    order_id                 UUID          NOT NULL,
    old_status               TEXT          NOT NULL,
    new_status               TEXT          NOT NULL,
    reason                   TEXT          NULL,
    changed_by               UUID          NULL,
    changed_at               TIMESTAMPTZ   NOT NULL,
    PRIMARY KEY (order_status_history_id),
    CONSTRAINT fk_order_status_history_order FOREIGN KEY (order_id) REFERENCES orders.orders (order_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_order_status_history_order ON orders.order_status_history (order_id);

-- TRACEABILITY C50: table_mgmt.tables.current_order_id is a soft cache pointer
-- created FK-less in 010; ownership truth is orders.orders.table_id. The FK is
-- added here once orders.orders exists (forward-reference safe, Phase A).
ALTER TABLE table_mgmt.tables
    ADD CONSTRAINT fk_tables_current_order
    FOREIGN KEY (current_order_id) REFERENCES orders.orders (order_id);