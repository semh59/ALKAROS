DROP TABLE IF EXISTS invoicing.invoice_line_sources;
DROP FUNCTION IF EXISTS invoicing.refuse_invoice_line_source_change();
DROP FUNCTION IF EXISTS invoicing.assert_invoice_trace_complete();
DROP INDEX IF EXISTS invoicing.ux_invoices_id_source_set;
