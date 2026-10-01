CREATE TABLE IF NOT EXISTS purchasing.purchase_invoices (
    invoice_id          UUID PRIMARY KEY,
    ettn                UUID NOT NULL,
    invoice_number      VARCHAR(64) NOT NULL,
    issue_date          DATE NOT NULL,
    supplier_tax_number VARCHAR(32) NOT NULL,
    supplier_name       VARCHAR(250) NOT NULL,
    supplier_id         UUID NULL REFERENCES purchasing.suppliers(supplier_id),
    currency            VARCHAR(3) NOT NULL,
    source              VARCHAR(32) NOT NULL,
    status              VARCHAR(32) NOT NULL DEFAULT 'Draft',
    imported_by         VARCHAR(100) NOT NULL,
    raw_xml             TEXT NOT NULL,
    row_version         BIGINT NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_purchase_invoices_ettn UNIQUE (ettn),
    CONSTRAINT ck_purchase_invoices_source CHECK (source IN ('XmlUpload', 'QnbInbox')),
    CONSTRAINT ck_purchase_invoices_status CHECK (status IN ('Draft', 'Approved', 'Rejected'))
);

CREATE INDEX IF NOT EXISTS idx_purchase_invoices_status_created ON purchasing.purchase_invoices(status, created_at DESC);

CREATE TABLE IF NOT EXISTS purchasing.purchase_invoice_lines (
    line_id              UUID PRIMARY KEY,
    invoice_id           UUID NOT NULL REFERENCES purchasing.purchase_invoices(invoice_id) ON DELETE CASCADE,
    line_number          INTEGER NOT NULL,
    item_key             VARCHAR(200) NOT NULL,
    supplier_item_code   VARCHAR(100) NULL,
    description          VARCHAR(500) NOT NULL,
    quantity             NUMERIC(18, 4) NOT NULL,
    unit_code            VARCHAR(16) NOT NULL,
    unit_price           NUMERIC(18, 6) NOT NULL,
    line_net             NUMERIC(18, 4) NOT NULL,
    stock_item_id        UUID NULL,
    conversion_factor    NUMERIC(18, 6) NULL,
    CONSTRAINT uq_purchase_invoice_lines_number UNIQUE (invoice_id, line_number),
    CONSTRAINT ck_purchase_invoice_lines_quantity CHECK (quantity > 0),
    CONSTRAINT ck_purchase_invoice_lines_price CHECK (unit_price >= 0),
    CONSTRAINT ck_purchase_invoice_lines_mapping CHECK ((stock_item_id IS NULL) = (conversion_factor IS NULL)),
    CONSTRAINT ck_purchase_invoice_lines_factor CHECK (conversion_factor IS NULL OR conversion_factor > 0)
);

CREATE TABLE IF NOT EXISTS purchasing.supplier_item_mappings (
    mapping_id           UUID PRIMARY KEY,
    supplier_tax_number  VARCHAR(32) NOT NULL,
    item_key             VARCHAR(200) NOT NULL,
    purchase_unit_code   VARCHAR(16) NOT NULL,
    stock_item_id        UUID NOT NULL,
    conversion_factor    NUMERIC(18, 6) NOT NULL,
    updated_at           TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_supplier_item_mappings_key UNIQUE (supplier_tax_number, item_key, purchase_unit_code),
    CONSTRAINT ck_supplier_item_mappings_factor CHECK (conversion_factor > 0)
);
