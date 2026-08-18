CREATE TABLE IF NOT EXISTS table_mgmt.table_merges (
    table_merge_id     UUID        NOT NULL,
    merge_group_id     UUID        NOT NULL,
    primary_table_id   UUID        NOT NULL,
    merged_table_id    UUID        NOT NULL,
    original_order_id  UUID        NULL,
    original_bill_id   UUID        NULL,
    status             TEXT        NOT NULL CHECK (status IN ('Active', 'Unmerged')),
    reason             TEXT        NOT NULL,
    merged_by          UUID        NOT NULL,
    merged_at          TIMESTAMPTZ NOT NULL,
    unmerged_at        TIMESTAMPTZ NULL,
    unmerged_by        UUID        NULL,
    unmerge_reason     TEXT        NULL,
    row_version        BIGINT      NOT NULL DEFAULT 1,
    PRIMARY KEY (table_merge_id),
    CONSTRAINT fk_table_merges_primary FOREIGN KEY (primary_table_id) REFERENCES table_mgmt.tables (table_id),
    CONSTRAINT fk_table_merges_merged FOREIGN KEY (merged_table_id) REFERENCES table_mgmt.tables (table_id),
    CONSTRAINT fk_table_merges_order FOREIGN KEY (original_order_id) REFERENCES orders.orders (order_id),
    CONSTRAINT fk_table_merges_bill FOREIGN KEY (original_bill_id) REFERENCES billing.bills (bill_id),
    CONSTRAINT fk_table_merges_merged_by FOREIGN KEY (merged_by) REFERENCES identity.users (user_id),
    CONSTRAINT fk_table_merges_unmerged_by FOREIGN KEY (unmerged_by) REFERENCES identity.users (user_id)
);

CREATE INDEX IF NOT EXISTS ix_table_merges_group ON table_mgmt.table_merges (merge_group_id);
CREATE INDEX IF NOT EXISTS ix_table_merges_primary ON table_mgmt.table_merges (primary_table_id);
CREATE INDEX IF NOT EXISTS ix_table_merges_merged ON table_mgmt.table_merges (merged_table_id);
CREATE INDEX IF NOT EXISTS ix_table_merges_status ON table_mgmt.table_merges (status);
