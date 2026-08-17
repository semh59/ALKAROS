CREATE SCHEMA IF NOT EXISTS billing;

CREATE TABLE IF NOT EXISTS billing.bills (
    bill_id              UUID          NOT NULL,
    bill_number          VARCHAR(50)   NOT NULL,
    table_id             UUID          NULL,
    order_id             UUID          NULL,
    customer_account_id  UUID          NULL,
    status               TEXT          NOT NULL CHECK (status IN ('Open', 'PartiallyAllocated', 'Allocated', 'PartiallyPaid', 'Paid', 'Cancelled', 'Reopened')),
    subtotal             NUMERIC(18,2) NOT NULL DEFAULT 0,
    discount_total       NUMERIC(18,2) NOT NULL DEFAULT 0,
    tax_total            NUMERIC(18,2) NOT NULL DEFAULT 0,
    payable_amount       NUMERIC(18,2) NOT NULL DEFAULT 0,
    allocated_amount     NUMERIC(18,2) NOT NULL DEFAULT 0,
    paid_amount          NUMERIC(18,2) NOT NULL DEFAULT 0,
    change_amount        NUMERIC(18,2) NOT NULL DEFAULT 0,
    currency_code        CHAR(3)       NOT NULL DEFAULT 'TRY',
    opened_at            TIMESTAMPTZ   NOT NULL,
    closed_at            TIMESTAMPTZ   NULL,
    cancelled_at         TIMESTAMPTZ   NULL,
    reopened_at          TIMESTAMPTZ   NULL,
    created_at           TIMESTAMPTZ   NOT NULL,
    updated_at           TIMESTAMPTZ   NOT NULL,
    row_version          BIGINT        NOT NULL DEFAULT 1,
    PRIMARY KEY (bill_id),
    UNIQUE (bill_number),
    CONSTRAINT fk_bills_table FOREIGN KEY (table_id) REFERENCES table_mgmt.tables (table_id),
    CONSTRAINT fk_bills_order FOREIGN KEY (order_id) REFERENCES orders.orders (order_id)
);

CREATE INDEX IF NOT EXISTS ix_bills_table ON billing.bills (table_id);
CREATE INDEX IF NOT EXISTS ix_bills_order ON billing.bills (order_id);

CREATE TABLE IF NOT EXISTS billing.bill_items (
    bill_item_id          UUID          NOT NULL,
    bill_id               UUID          NOT NULL,
    order_item_id         UUID          NOT NULL,
    product_id            UUID          NOT NULL,
    product_name_snapshot TEXT          NOT NULL,
    quantity              NUMERIC(18,3) NOT NULL,
    unit_price            NUMERIC(18,2) NOT NULL,
    discount_amount       NUMERIC(18,2) NOT NULL DEFAULT 0,
    tax_rate              NUMERIC(5,2)  NOT NULL,
    tax_amount            NUMERIC(18,2) NOT NULL DEFAULT 0,
    net_amount            NUMERIC(18,2) NOT NULL,
    gross_amount          NUMERIC(18,2) NOT NULL,
    line_type             TEXT          NOT NULL CHECK (line_type IN ('Sale', 'Discount', 'Complimentary', 'Refund', 'Waste', 'Adjustment')),
    notes                 TEXT          NULL,
    created_at            TIMESTAMPTZ   NOT NULL,
    updated_at            TIMESTAMPTZ   NOT NULL,
    row_version           BIGINT        NOT NULL DEFAULT 1,
    PRIMARY KEY (bill_item_id),
    UNIQUE (order_item_id),
    CONSTRAINT fk_bill_items_bill FOREIGN KEY (bill_id) REFERENCES billing.bills (bill_id) ON DELETE CASCADE,
    CONSTRAINT fk_bill_items_order_item FOREIGN KEY (order_item_id) REFERENCES orders.order_items (order_item_id),
    CONSTRAINT fk_bill_items_product FOREIGN KEY (product_id) REFERENCES catalog.products (product_id)
);

CREATE INDEX IF NOT EXISTS ix_bill_items_bill ON billing.bill_items (bill_id);
CREATE INDEX IF NOT EXISTS ix_bill_items_product ON billing.bill_items (product_id);
