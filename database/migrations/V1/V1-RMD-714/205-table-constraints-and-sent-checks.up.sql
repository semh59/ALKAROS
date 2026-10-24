-- At most one active reservation per table and one active merge row per participant table:
-- the application checks these under a row lock, the indexes make a race impossible.
CREATE UNIQUE INDEX IF NOT EXISTS ux_table_reservations_active_table
    ON table_mgmt.table_reservations (table_id)
    WHERE status = 'Active';

CREATE UNIQUE INDEX IF NOT EXISTS ux_table_merges_active_participant
    ON table_mgmt.table_merges (merged_table_id)
    WHERE status = 'Active';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_table_reservations_party_size') THEN
        ALTER TABLE table_mgmt.table_reservations
            ADD CONSTRAINT ck_table_reservations_party_size CHECK (party_size >= 1);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_table_reservations_expiry') THEN
        ALTER TABLE table_mgmt.table_reservations
            ADD CONSTRAINT ck_table_reservations_expiry CHECK (expires_at IS NULL OR expires_at > reserved_at);
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_table_merges_distinct_tables') THEN
        ALTER TABLE table_mgmt.table_merges
            ADD CONSTRAINT ck_table_merges_distinct_tables CHECK (primary_table_id <> merged_table_id);
    END IF;
END
$$;

-- A check the waiter sent to the till leaves its table's pointer but keeps pointing at the table
-- through orders.table_id (the till shows where it came from). This record marks it as no longer
-- part of the table, so a later transfer or merge of the table does not drag it along.
CREATE TABLE IF NOT EXISTS table_mgmt.table_sent_checks (
    order_id UUID        NOT NULL,
    table_id UUID        NOT NULL,
    sent_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (order_id),
    CONSTRAINT fk_table_sent_checks_order FOREIGN KEY (order_id) REFERENCES orders.orders (order_id),
    CONSTRAINT fk_table_sent_checks_table FOREIGN KEY (table_id) REFERENCES table_mgmt.tables (table_id)
);

CREATE INDEX IF NOT EXISTS ix_table_sent_checks_table ON table_mgmt.table_sent_checks (table_id);
