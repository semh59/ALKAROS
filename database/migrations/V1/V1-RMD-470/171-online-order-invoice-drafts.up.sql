-- V1-RMD-470: e-Arsiv invoice drafts for online (platform) orders, one per order.
-- Menu prices are tax-inclusive, so a line's net + tax = gross exactly. The seller is copied onto the invoice when it
-- is drafted, so later profile edits never change it. The buyer of a platform order is a final consumer (the customer's
-- details stay in the sealed inbox payload). A draft is immutable apart from its status; nothing here numbers or sends it.

CREATE TABLE IF NOT EXISTS invoicing.seller_profile (
    singleton      BOOLEAN     NOT NULL PRIMARY KEY DEFAULT true CHECK (singleton),
    legal_name     TEXT        NOT NULL CHECK (length(btrim(legal_name)) > 0),
    tax_id_kind    TEXT        NOT NULL CHECK (tax_id_kind IN ('Vkn', 'Tckn')),
    tax_id_number  TEXT        NOT NULL,
    tax_office     TEXT        NOT NULL CHECK (length(btrim(tax_office)) > 0),
    address        TEXT        NOT NULL CHECK (length(btrim(address)) > 0),
    district       TEXT        NOT NULL CHECK (length(btrim(district)) > 0),
    city           TEXT        NOT NULL CHECK (length(btrim(city)) > 0),
    email          TEXT        NULL,
    updated_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by     UUID        NULL,
    CONSTRAINT ck_seller_profile_tax_id CHECK (
        (tax_id_kind = 'Vkn' AND tax_id_number ~ '^[0-9]{10}$')
        OR (tax_id_kind = 'Tckn' AND tax_id_number ~ '^[0-9]{11}$'))
);

CREATE TABLE IF NOT EXISTS invoicing.order_invoices (
    invoice_id             UUID           NOT NULL PRIMARY KEY,
    order_id               UUID           NOT NULL,
    provider               TEXT           NOT NULL CHECK (provider IN ('yemeksepeti', 'trendyol-go')),
    external_order_id      TEXT           NOT NULL CHECK (length(btrim(external_order_id)) > 0),
    order_number           TEXT           NOT NULL CHECK (length(btrim(order_number)) > 0),
    profile                TEXT           NOT NULL CHECK (profile = 'EArsiv'),
    ubl_profile_id         TEXT           NOT NULL CHECK (ubl_profile_id = 'EARSIVFATURA'),
    invoice_type_code      TEXT           NOT NULL CHECK (invoice_type_code = 'SATIS'),
    currency_code          CHAR(3)        NOT NULL CHECK (currency_code = 'TRY'),
    issue_date             DATE           NOT NULL,
    service_date           DATE           NOT NULL,
    status                 TEXT           NOT NULL DEFAULT 'Draft' CHECK (status IN ('Draft')),
    buyer_kind             TEXT           NOT NULL CHECK (buyer_kind = 'FinalConsumer'),
    seller_legal_name      TEXT           NOT NULL,
    seller_tax_id_kind     TEXT           NOT NULL CHECK (seller_tax_id_kind IN ('Vkn', 'Tckn')),
    seller_tax_id_number   TEXT           NOT NULL,
    seller_tax_office      TEXT           NOT NULL,
    seller_address         TEXT           NOT NULL,
    web_address            TEXT           NOT NULL CHECK (length(btrim(web_address)) > 0),
    payment_method         TEXT           NULL,
    payment_date           DATE           NULL,
    carrier_name           TEXT           NULL,
    carrier_tax_id         TEXT           NULL,
    line_extension_amount  NUMERIC(18, 2) NOT NULL CHECK (line_extension_amount >= 0),
    tax_total              NUMERIC(18, 2) NOT NULL CHECK (tax_total >= 0),
    payable_amount         NUMERIC(18, 2) NOT NULL CHECK (payable_amount > 0),
    created_at             TIMESTAMPTZ    NOT NULL DEFAULT now(),
    created_by             UUID           NULL,
    CONSTRAINT ck_order_invoices_totals CHECK (line_extension_amount + tax_total = payable_amount)
);

-- An order is invoiced at most once: a retry finds this row.
CREATE UNIQUE INDEX IF NOT EXISTS ux_order_invoices_order ON invoicing.order_invoices (order_id);
CREATE INDEX IF NOT EXISTS ix_order_invoices_issue_date ON invoicing.order_invoices (issue_date, created_at);

CREATE TABLE IF NOT EXISTS invoicing.order_invoice_lines (
    invoice_id    UUID           NOT NULL REFERENCES invoicing.order_invoices (invoice_id),
    line_number   INTEGER        NOT NULL CHECK (line_number > 0),
    description   TEXT           NOT NULL CHECK (length(btrim(description)) > 0),
    quantity      NUMERIC(18, 3) NOT NULL CHECK (quantity > 0),
    unit_code     TEXT           NOT NULL,
    tax_rate      NUMERIC(5, 2)  NOT NULL CHECK (tax_rate >= 0),
    net_amount    NUMERIC(18, 2) NOT NULL CHECK (net_amount >= 0),
    tax_amount    NUMERIC(18, 2) NOT NULL CHECK (tax_amount >= 0),
    gross_amount  NUMERIC(18, 2) NOT NULL CHECK (gross_amount > 0),
    PRIMARY KEY (invoice_id, line_number),
    CONSTRAINT ck_order_invoice_lines_amounts CHECK (net_amount + tax_amount = gross_amount)
);

CREATE OR REPLACE FUNCTION invoicing.refuse_order_invoice_line_change() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'order invoice lines are immutable (invoice %)', OLD.invoice_id
        USING ERRCODE = 'integrity_constraint_violation';
END;
$$;

DROP TRIGGER IF EXISTS tr_order_invoice_lines_immutable ON invoicing.order_invoice_lines;
CREATE TRIGGER tr_order_invoice_lines_immutable
    BEFORE UPDATE OR DELETE ON invoicing.order_invoice_lines
    FOR EACH ROW EXECUTE FUNCTION invoicing.refuse_order_invoice_line_change();

CREATE OR REPLACE FUNCTION invoicing.refuse_order_invoice_content_change() RETURNS trigger
LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'order invoices are never deleted (invoice %)', OLD.invoice_id
            USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    IF (to_jsonb(NEW) - 'status') IS DISTINCT FROM (to_jsonb(OLD) - 'status') THEN
        RAISE EXCEPTION 'order invoice content is immutable (invoice %)', OLD.invoice_id
            USING ERRCODE = 'integrity_constraint_violation';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS tr_order_invoices_immutable ON invoicing.order_invoices;
CREATE TRIGGER tr_order_invoices_immutable
    BEFORE UPDATE OR DELETE ON invoicing.order_invoices
    FOR EACH ROW EXECUTE FUNCTION invoicing.refuse_order_invoice_content_change();
