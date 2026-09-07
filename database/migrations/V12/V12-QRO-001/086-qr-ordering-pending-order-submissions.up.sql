-- V12-QRO-001: idempotency ledger for QR order intake. Guards against
-- re-publishing the same QrOrderSubmitted event twice on a client retry
-- (a dropped connection resend with the identical client-generated
-- submission id); the eventual Order row's own uniqueness
-- (ux_orders_table_submission, V1-RMD-123) is a second, independent guard
-- on the consumer side. items_snapshot durably records the price/name
-- snapshot taken at submission time — the same data carried in the
-- outbox event payload, kept here for audit/debugging after the outbox
-- row is eventually pruned.
CREATE TABLE IF NOT EXISTS qr_ordering.pending_order_submissions (
    submission_id        UUID          NOT NULL,
    table_id              UUID          NOT NULL,
    customer_session_id   UUID          NOT NULL,
    items_snapshot        JSONB         NOT NULL,
    submitted_at          TIMESTAMPTZ   NOT NULL,
    PRIMARY KEY (submission_id),
    CONSTRAINT fk_pending_order_submissions_table
        FOREIGN KEY (table_id) REFERENCES table_mgmt.tables (table_id),
    CONSTRAINT fk_pending_order_submissions_session
        FOREIGN KEY (customer_session_id) REFERENCES qr_ordering.customer_sessions (session_id)
);

CREATE INDEX IF NOT EXISTS ix_pending_order_submissions_table ON qr_ordering.pending_order_submissions (table_id);
