CREATE TABLE IF NOT EXISTS table_mgmt.table_reservations (
    table_reservation_id UUID        NOT NULL,
    table_id             UUID        NOT NULL,
    order_id             UUID        NULL,
    actor_id             UUID        NULL,
    actor_type           TEXT        NOT NULL CHECK (actor_type IN ('User', 'Device', 'Customer', 'System')),
    status               TEXT        NOT NULL CHECK (status IN ('Active', 'Claimed', 'Cancelled', 'Expired')),
    reason               TEXT        NOT NULL,
    party_size           INT         NOT NULL DEFAULT 1,
    reserved_at          TIMESTAMPTZ NOT NULL,
    expires_at           TIMESTAMPTZ NULL,
    released_at          TIMESTAMPTZ NULL,
    released_by          UUID        NULL,
    release_reason       TEXT        NULL,
    row_version          BIGINT      NOT NULL DEFAULT 1,
    PRIMARY KEY (table_reservation_id),
    CONSTRAINT fk_table_reservations_table FOREIGN KEY (table_id) REFERENCES table_mgmt.tables (table_id),
    CONSTRAINT fk_table_reservations_order FOREIGN KEY (order_id) REFERENCES orders.orders (order_id),
    CONSTRAINT fk_table_reservations_actor FOREIGN KEY (actor_id) REFERENCES identity.users (user_id),
    CONSTRAINT fk_table_reservations_released_by FOREIGN KEY (released_by) REFERENCES identity.users (user_id)
);

CREATE INDEX IF NOT EXISTS ix_table_reservations_table ON table_mgmt.table_reservations (table_id);
CREATE INDEX IF NOT EXISTS ix_table_reservations_status ON table_mgmt.table_reservations (status);
CREATE INDEX IF NOT EXISTS ix_table_reservations_order ON table_mgmt.table_reservations (order_id);
CREATE INDEX IF NOT EXISTS ix_table_reservations_expires ON table_mgmt.table_reservations (expires_at) WHERE status = 'Active';
