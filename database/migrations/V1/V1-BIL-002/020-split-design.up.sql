CREATE TABLE IF NOT EXISTS billing.bill_allocations (
    bill_allocation_id  UUID          NOT NULL,
    bill_id             UUID          NOT NULL,
    bill_item_id        UUID          NULL,
    owner_type          TEXT          NOT NULL CHECK (owner_type IN ('Person', 'Item', 'Amount', 'CustomerAccount')),
    owner_reference     TEXT          NOT NULL,
    allocated_quantity  NUMERIC(18,3) NULL,
    allocated_amount    NUMERIC(18,2) NOT NULL,
    tax_amount          NUMERIC(18,2) NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ   NOT NULL,
    created_by          UUID          NULL,
    row_version         BIGINT        NOT NULL DEFAULT 1,
    PRIMARY KEY (bill_allocation_id),
    CONSTRAINT fk_bill_allocations_bill FOREIGN KEY (bill_id) REFERENCES billing.bills (bill_id) ON DELETE CASCADE,
    CONSTRAINT fk_bill_allocations_item FOREIGN KEY (bill_item_id) REFERENCES billing.bill_items (bill_item_id) ON DELETE CASCADE,
    CONSTRAINT chk_allocated_amount_positive CHECK (allocated_amount > 0),
    CONSTRAINT chk_allocated_quantity_positive CHECK (allocated_quantity IS NULL OR allocated_quantity > 0)
);

CREATE INDEX IF NOT EXISTS ix_bill_allocations_bill ON billing.bill_allocations (bill_id);
CREATE INDEX IF NOT EXISTS ix_bill_allocations_item ON billing.bill_allocations (bill_item_id);
