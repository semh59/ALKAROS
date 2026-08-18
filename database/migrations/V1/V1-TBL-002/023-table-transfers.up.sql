CREATE TABLE IF NOT EXISTS table_mgmt.table_transfers (
    table_transfer_id UUID        NOT NULL,
    source_table_id   UUID        NOT NULL,
    target_table_id   UUID        NOT NULL,
    order_id          UUID        NULL,
    bill_id           UUID        NULL,
    reason            TEXT        NOT NULL,
    transferred_by    UUID        NOT NULL,
    transferred_at    TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (table_transfer_id),
    CONSTRAINT fk_table_transfers_source FOREIGN KEY (source_table_id) REFERENCES table_mgmt.tables (table_id),
    CONSTRAINT fk_table_transfers_target FOREIGN KEY (target_table_id) REFERENCES table_mgmt.tables (table_id),
    CONSTRAINT fk_table_transfers_order FOREIGN KEY (order_id) REFERENCES orders.orders (order_id),
    CONSTRAINT fk_table_transfers_bill FOREIGN KEY (bill_id) REFERENCES billing.bills (bill_id),
    CONSTRAINT fk_table_transfers_user FOREIGN KEY (transferred_by) REFERENCES identity.users (user_id)
);

CREATE INDEX IF NOT EXISTS ix_table_transfers_source ON table_mgmt.table_transfers (source_table_id);
CREATE INDEX IF NOT EXISTS ix_table_transfers_target ON table_mgmt.table_transfers (target_table_id);
CREATE INDEX IF NOT EXISTS ix_table_transfers_transferred_at ON table_mgmt.table_transfers (transferred_at);
