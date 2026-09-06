CREATE TABLE IF NOT EXISTS purchasing.purchase_orders (
    order_id                UUID PRIMARY KEY,
    order_number            VARCHAR(64) NOT NULL UNIQUE,
    supplier_id             UUID NOT NULL REFERENCES purchasing.suppliers(supplier_id),
    status                  VARCHAR(32) NOT NULL,
    destination_location_id UUID NOT NULL,
    notes                   TEXT NULL,
    total_amount            NUMERIC(18, 4) NOT NULL DEFAULT 0,
    currency                VARCHAR(3) NOT NULL DEFAULT 'TRY',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_purchase_orders_supplier ON purchasing.purchase_orders(supplier_id);
CREATE INDEX IF NOT EXISTS idx_purchase_orders_status ON purchasing.purchase_orders(status);

CREATE TABLE IF NOT EXISTS purchasing.purchase_order_lines (
    line_id                 UUID PRIMARY KEY,
    order_id                UUID NOT NULL REFERENCES purchasing.purchase_orders(order_id) ON DELETE CASCADE,
    stock_item_id           UUID NOT NULL,
    ordered_quantity        NUMERIC(18, 4) NOT NULL,
    received_quantity       NUMERIC(18, 4) NOT NULL DEFAULT 0,
    unit_code               VARCHAR(16) NOT NULL,
    unit_price              NUMERIC(18, 4) NOT NULL,
    total_price             NUMERIC(18, 4) NOT NULL,
    status                  VARCHAR(32) NOT NULL DEFAULT 'Pending',
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_po_lines_order ON purchasing.purchase_order_lines(order_id);
CREATE INDEX IF NOT EXISTS idx_po_lines_item ON purchasing.purchase_order_lines(stock_item_id);

CREATE TABLE IF NOT EXISTS purchasing.goods_receipts (
    receipt_id              UUID PRIMARY KEY,
    receipt_number          VARCHAR(64) NOT NULL UNIQUE,
    order_id                UUID NOT NULL REFERENCES purchasing.purchase_orders(order_id),
    supplier_id             UUID NOT NULL REFERENCES purchasing.suppliers(supplier_id),
    destination_location_id UUID NOT NULL,
    received_at             TIMESTAMPTZ NOT NULL DEFAULT now(),
    received_by             VARCHAR(100) NOT NULL,
    approved_by             VARCHAR(100) NULL,
    notes                   TEXT NULL,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_goods_receipts_order ON purchasing.goods_receipts(order_id);
CREATE INDEX IF NOT EXISTS idx_goods_receipts_supplier ON purchasing.goods_receipts(supplier_id);

CREATE TABLE IF NOT EXISTS purchasing.goods_receipt_items (
    item_id                 UUID PRIMARY KEY,
    receipt_id              UUID NOT NULL REFERENCES purchasing.goods_receipts(receipt_id) ON DELETE CASCADE,
    order_line_id           UUID NOT NULL REFERENCES purchasing.purchase_order_lines(line_id),
    stock_item_id           UUID NOT NULL,
    delivered_quantity      NUMERIC(18, 4) NOT NULL,
    accepted_quantity       NUMERIC(18, 4) NOT NULL,
    rejected_quantity       NUMERIC(18, 4) NOT NULL DEFAULT 0,
    unit_code               VARCHAR(16) NOT NULL,
    unit_price              NUMERIC(18, 4) NOT NULL,
    variance_quantity       NUMERIC(18, 4) NOT NULL DEFAULT 0,
    variance_reason         TEXT NULL,
    is_approved_by_manager  BOOLEAN NOT NULL DEFAULT false,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_goods_receipt_items_receipt ON purchasing.goods_receipt_items(receipt_id);
