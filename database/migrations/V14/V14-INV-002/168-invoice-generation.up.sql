-- V14-INV-002: outgoing invoice drafts generated from a V14-INV-001 source set.
-- One invoice per source set; lines are one per KDV rate, tax-inclusive
-- (V0-CMP-002), with net + tax = gross exactly. The buyer snapshot is an
-- encrypted envelope (V0-CMP-003 "Invoice data"). A draft is immutable: its
-- lines never change and its header only ever changes status (later tasks
-- number, send or cancel it). Nothing here writes the account ledger, so
-- generation never adds a second debit (V0-DOM-007 invariant 1).

CREATE TABLE IF NOT EXISTS invoicing.invoices (
    invoice_id             UUID           NOT NULL PRIMARY KEY,
    source_set_id          UUID           NOT NULL REFERENCES invoicing.invoice_source_sets (source_set_id),
    period_id              UUID           NOT NULL REFERENCES invoicing.invoice_periods (period_id),
    customer_id            UUID           NOT NULL,
    profile                TEXT           NOT NULL CHECK (profile IN ('EFatura', 'EArsiv')),
    ubl_profile_id         TEXT           NOT NULL CHECK (ubl_profile_id IN ('TEMELFATURA', 'EARSIVFATURA')),
    invoice_type_code      TEXT           NOT NULL CHECK (invoice_type_code = 'SATIS'),
    currency_code          CHAR(3)        NOT NULL CHECK (currency_code = 'TRY'),
    issue_date             DATE           NOT NULL,
    status                 TEXT           NOT NULL DEFAULT 'Draft' CHECK (status IN ('Draft')),
    buyer_envelope         BYTEA          NOT NULL,
    line_extension_amount  NUMERIC(18, 2) NOT NULL CHECK (line_extension_amount >= 0),
    tax_total              NUMERIC(18, 2) NOT NULL CHECK (tax_total >= 0),
    payable_amount         NUMERIC(18, 2) NOT NULL CHECK (payable_amount > 0),
    created_at             TIMESTAMPTZ    NOT NULL DEFAULT now(),
    created_by             UUID           NOT NULL,
    CONSTRAINT ck_invoices_totals CHECK (line_extension_amount + tax_total = payable_amount),
    CONSTRAINT ck_invoices_profile_pair CHECK (
        (profile = 'EFatura' AND ubl_profile_id = 'TEMELFATURA')
        OR (profile = 'EArsiv' AND ubl_profile_id = 'EARSIVFATURA'))
);

-- A source set is invoiced at most once: a retry finds this row.
CREATE UNIQUE INDEX IF NOT EXISTS ux_invoices_source_set ON invoicing.invoices (source_set_id);
CREATE INDEX IF NOT EXISTS ix_invoices_customer ON invoicing.invoices (customer_id, issue_date);

CREATE TABLE IF NOT EXISTS invoicing.invoice_lines (
    invoice_id    UUID           NOT NULL REFERENCES invoicing.invoices (invoice_id),
    line_number   INTEGER        NOT NULL CHECK (line_number > 0),
    description   TEXT           NOT NULL CHECK (length(btrim(description)) > 0),
    quantity      NUMERIC(18, 3) NOT NULL CHECK (quantity > 0),
    unit_code     TEXT           NOT NULL,
    tax_rate      NUMERIC(5, 2)  NOT NULL CHECK (tax_rate >= 0),
    net_amount    NUMERIC(18, 2) NOT NULL CHECK (net_amount >= 0),
    tax_amount    NUMERIC(18, 2) NOT NULL CHECK (tax_amount >= 0),
    gross_amount  NUMERIC(18, 2) NOT NULL CHECK (gross_amount > 0),
    PRIMARY KEY (invoice_id, line_number),
    CONSTRAINT ux_invoice_lines_rate UNIQUE (invoice_id, tax_rate),
    CONSTRAINT ck_invoice_lines_amounts CHECK (net_amount + tax_amount = gross_amount)
);

CREATE OR REPLACE FUNCTION invoicing.refuse_invoice_line_change() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'invoice lines are immutable (invoice %)', OLD.invoice_id
        USING ERRCODE = 'integrity_constraint_violation';
END;
$$;

DROP TRIGGER IF EXISTS tr_invoice_lines_immutable ON invoicing.invoice_lines;
CREATE TRIGGER tr_invoice_lines_immutable
    BEFORE UPDATE OR DELETE ON invoicing.invoice_lines
    FOR EACH ROW EXECUTE FUNCTION invoicing.refuse_invoice_line_change();

CREATE OR REPLACE FUNCTION invoicing.refuse_invoice_content_change() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'invoices are never deleted (invoice %)', OLD.invoice_id
            USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    IF (NEW.invoice_id, NEW.source_set_id, NEW.period_id, NEW.customer_id, NEW.profile, NEW.ubl_profile_id,
        NEW.invoice_type_code, NEW.currency_code, NEW.issue_date, NEW.buyer_envelope, NEW.line_extension_amount,
        NEW.tax_total, NEW.payable_amount, NEW.created_at, NEW.created_by)
       IS DISTINCT FROM
       (OLD.invoice_id, OLD.source_set_id, OLD.period_id, OLD.customer_id, OLD.profile, OLD.ubl_profile_id,
        OLD.invoice_type_code, OLD.currency_code, OLD.issue_date, OLD.buyer_envelope, OLD.line_extension_amount,
        OLD.tax_total, OLD.payable_amount, OLD.created_at, OLD.created_by) THEN
        RAISE EXCEPTION 'invoice content is immutable (invoice %)', OLD.invoice_id
            USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS tr_invoices_immutable ON invoicing.invoices;
CREATE TRIGGER tr_invoices_immutable
    BEFORE UPDATE OR DELETE ON invoicing.invoices
    FOR EACH ROW EXECUTE FUNCTION invoicing.refuse_invoice_content_change();
