ALTER TABLE purchasing.purchase_invoices DROP CONSTRAINT IF EXISTS ck_purchase_invoices_kind;
ALTER TABLE purchasing.purchase_invoices DROP COLUMN IF EXISTS referenced_invoice_number;
ALTER TABLE purchasing.purchase_invoices DROP COLUMN IF EXISTS kind;
