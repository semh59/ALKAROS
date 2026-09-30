-- V14-INV-003: which ledger transactions each invoice line was built from.
-- A charge is split across the KDV rates of the bill it paid, so a line
-- draws on several charges and a charge feeds several lines: one row per
-- (line, charge) with the gross amount the charge contributed. The links of
-- an invoice are written in one transaction and are complete at commit:
-- every line equals the sum of its links and every live Charge of the source
-- set is spread over the lines in full. Rows are never changed or removed.

-- Lets a link prove that its source set is the invoice's own.
CREATE UNIQUE INDEX IF NOT EXISTS ux_invoices_id_source_set
    ON invoicing.invoices (invoice_id, source_set_id);

CREATE TABLE IF NOT EXISTS invoicing.invoice_line_sources (
    invoice_id        UUID           NOT NULL,
    line_number       INTEGER        NOT NULL,
    source_set_id     UUID           NOT NULL,
    transaction_id    UUID           NOT NULL,
    allocated_amount  NUMERIC(18, 2) NOT NULL CHECK (allocated_amount > 0),
    recorded_at       TIMESTAMPTZ    NOT NULL DEFAULT now(),
    recorded_by       UUID           NOT NULL,
    PRIMARY KEY (invoice_id, line_number, transaction_id),
    FOREIGN KEY (invoice_id, line_number) REFERENCES invoicing.invoice_lines (invoice_id, line_number),
    FOREIGN KEY (invoice_id, source_set_id) REFERENCES invoicing.invoices (invoice_id, source_set_id),
    FOREIGN KEY (source_set_id, transaction_id) REFERENCES invoicing.invoice_source_lines (source_set_id, transaction_id)
);

CREATE INDEX IF NOT EXISTS ix_invoice_line_sources_transaction
    ON invoicing.invoice_line_sources (source_set_id, transaction_id);

CREATE OR REPLACE FUNCTION invoicing.refuse_invoice_line_source_change() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'invoice line sources are immutable (invoice %)', OLD.invoice_id
        USING ERRCODE = 'integrity_constraint_violation';
END;
$$;

DROP TRIGGER IF EXISTS tr_invoice_line_sources_immutable ON invoicing.invoice_line_sources;
CREATE TRIGGER tr_invoice_line_sources_immutable
    BEFORE UPDATE OR DELETE ON invoicing.invoice_line_sources
    FOR EACH ROW EXECUTE FUNCTION invoicing.refuse_invoice_line_source_change();

CREATE OR REPLACE FUNCTION invoicing.assert_invoice_trace_complete() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM invoicing.invoice_lines l
        WHERE l.invoice_id = NEW.invoice_id
          AND l.gross_amount IS DISTINCT FROM COALESCE((
              SELECT SUM(s.allocated_amount)
              FROM invoicing.invoice_line_sources s
              WHERE s.invoice_id = l.invoice_id AND s.line_number = l.line_number), 0)) THEN
        RAISE EXCEPTION 'invoice % has a line whose sources do not add up to its amount', NEW.invoice_id
            USING ERRCODE = 'check_violation';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM invoicing.invoices i
        JOIN invoicing.invoice_source_lines c ON c.source_set_id = i.source_set_id
        WHERE i.invoice_id = NEW.invoice_id
          AND c.transaction_type = 'Charge' AND NOT c.cancelled
          AND c.amount IS DISTINCT FROM COALESCE((
              SELECT SUM(s.allocated_amount)
              FROM invoicing.invoice_line_sources s
              WHERE s.invoice_id = i.invoice_id AND s.transaction_id = c.transaction_id), 0)) THEN
        RAISE EXCEPTION 'invoice % leaves a selected charge unrepresented or partly represented', NEW.invoice_id
            USING ERRCODE = 'check_violation';
    END IF;
    RETURN NULL;
END;
$$;

DROP TRIGGER IF EXISTS tr_invoice_line_sources_complete ON invoicing.invoice_line_sources;
CREATE CONSTRAINT TRIGGER tr_invoice_line_sources_complete
    AFTER INSERT ON invoicing.invoice_line_sources
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW EXECUTE FUNCTION invoicing.assert_invoice_trace_complete();
