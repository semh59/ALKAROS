-- The bill items (and how many of each) a refund request pays back, and whether they go back to stock.
CREATE TABLE IF NOT EXISTS payments.refund_request_items (
    refund_request_id UUID          NOT NULL,
    bill_item_id      UUID          NOT NULL,
    quantity          NUMERIC(18,3) NOT NULL,
    restore_stock     BOOLEAN       NOT NULL,
    PRIMARY KEY (refund_request_id, bill_item_id),
    CONSTRAINT fk_refund_request_items_request FOREIGN KEY (refund_request_id) REFERENCES payments.refund_requests (refund_request_id),
    CONSTRAINT fk_refund_request_items_bill_item FOREIGN KEY (bill_item_id) REFERENCES billing.bill_items (bill_item_id),
    CONSTRAINT ck_refund_request_items_quantity CHECK (quantity > 0)
);

CREATE INDEX IF NOT EXISTS ix_refund_request_items_bill_item ON payments.refund_request_items (bill_item_id);
