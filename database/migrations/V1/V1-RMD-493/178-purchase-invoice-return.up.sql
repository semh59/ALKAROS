ALTER TABLE purchasing.purchase_invoices ADD COLUMN IF NOT EXISTS kind VARCHAR(16) NOT NULL DEFAULT 'Invoice';
ALTER TABLE purchasing.purchase_invoices ADD COLUMN IF NOT EXISTS referenced_invoice_number VARCHAR(64) NULL;

ALTER TABLE purchasing.purchase_invoices DROP CONSTRAINT IF EXISTS ck_purchase_invoices_kind;
ALTER TABLE purchasing.purchase_invoices ADD CONSTRAINT ck_purchase_invoices_kind CHECK (kind IN ('Invoice', 'Return'));
