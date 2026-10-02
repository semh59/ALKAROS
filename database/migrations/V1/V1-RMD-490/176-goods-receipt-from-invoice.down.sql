-- Rolling back removes the invoice-sourced receipts (their stock movements stay in the ledger, which is append-only).
DELETE FROM purchasing.goods_receipt_items WHERE receipt_id IN (SELECT receipt_id FROM purchasing.goods_receipts WHERE order_id IS NULL);
DELETE FROM purchasing.goods_receipts WHERE order_id IS NULL;

DROP INDEX IF EXISTS purchasing.uq_goods_receipts_invoice;
ALTER TABLE purchasing.goods_receipts DROP CONSTRAINT IF EXISTS ck_goods_receipts_source;
ALTER TABLE purchasing.goods_receipts DROP COLUMN IF EXISTS invoice_id;
ALTER TABLE purchasing.goods_receipt_items ALTER COLUMN order_line_id SET NOT NULL;
ALTER TABLE purchasing.goods_receipts ALTER COLUMN order_id SET NOT NULL;
