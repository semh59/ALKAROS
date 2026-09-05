CREATE SCHEMA IF NOT EXISTS purchasing;

CREATE TABLE IF NOT EXISTS purchasing.suppliers (
    supplier_id     UUID PRIMARY KEY,
    code            VARCHAR(64) NOT NULL UNIQUE,
    name            VARCHAR(255) NOT NULL,
    tax_number      VARCHAR(32) NULL,
    tax_office      VARCHAR(100) NULL,
    phone           VARCHAR(50) NULL,
    email           VARCHAR(255) NULL,
    active          BOOLEAN NOT NULL DEFAULT true,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_suppliers_code ON purchasing.suppliers(code);
CREATE INDEX IF NOT EXISTS idx_suppliers_active ON purchasing.suppliers(active);
CREATE UNIQUE INDEX IF NOT EXISTS uq_suppliers_tax_number ON purchasing.suppliers(tax_number) WHERE tax_number IS NOT NULL;
