ALTER TABLE purchasing.goods_receipts ALTER COLUMN order_id DROP NOT NULL;
ALTER TABLE purchasing.goods_receipts ADD COLUMN IF NOT EXISTS invoice_id UUID NULL REFERENCES purchasing.purchase_invoices(invoice_id);
ALTER TABLE purchasing.goods_receipt_items ALTER COLUMN order_line_id DROP NOT NULL;

ALTER TABLE purchasing.goods_receipts DROP CONSTRAINT IF EXISTS ck_goods_receipts_source;
ALTER TABLE purchasing.goods_receipts ADD CONSTRAINT ck_goods_receipts_source CHECK (order_id IS NOT NULL OR invoice_id IS NOT NULL);

CREATE UNIQUE INDEX IF NOT EXISTS uq_goods_receipts_invoice ON purchasing.goods_receipts(invoice_id) WHERE invoice_id IS NOT NULL;
