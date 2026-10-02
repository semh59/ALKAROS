CREATE TABLE IF NOT EXISTS purchasing.purchase_invoice_inbox_cursors (
    source        VARCHAR(32) PRIMARY KEY,
    last_sequence BIGINT NOT NULL DEFAULT 0,
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_purchase_invoice_inbox_cursors_source CHECK (source IN ('QnbInbox')),
    CONSTRAINT ck_purchase_invoice_inbox_cursors_sequence CHECK (last_sequence >= 0)
);
