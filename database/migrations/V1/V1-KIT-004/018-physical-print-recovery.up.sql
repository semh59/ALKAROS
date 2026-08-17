CREATE SCHEMA IF NOT EXISTS kitchen;

CREATE TABLE IF NOT EXISTS kitchen.physical_print_deliveries (
    id                   UUID         PRIMARY KEY,
    print_job_id         UUID         NOT NULL REFERENCES kitchen.print_jobs(id) ON DELETE RESTRICT,
    ticket_id            UUID         NOT NULL REFERENCES kitchen.kitchen_tickets(id) ON DELETE RESTRICT,
    printer_id           UUID         NOT NULL REFERENCES kitchen.printers(id) ON DELETE RESTRICT,
    status               VARCHAR(32)  NOT NULL CHECK (status IN ('InFlight', 'Printed', 'Unknown', 'ReprintApproved', 'ReprintRejected', 'Reprinted')),
    attempt_number       INT          NOT NULL DEFAULT 1,
    is_reprint           BOOLEAN      NOT NULL DEFAULT FALSE,
    operator_id          VARCHAR(128) NULL,
    operator_reason      TEXT         NULL,
    crash_window_reason  TEXT         NULL,
    payload_snapshot     TEXT         NOT NULL,
    reprint_payload      TEXT         NULL,
    created_at           TIMESTAMPTZ  NOT NULL DEFAULT now(),
    delivered_at         TIMESTAMPTZ  NULL,
    resolved_at          TIMESTAMPTZ  NULL,
    row_version          BIGINT       NOT NULL DEFAULT 1
);

CREATE INDEX IF NOT EXISTS ix_kitchen_print_deliveries_job
    ON kitchen.physical_print_deliveries (print_job_id);

CREATE INDEX IF NOT EXISTS ix_kitchen_print_deliveries_ticket
    ON kitchen.physical_print_deliveries (ticket_id);

CREATE INDEX IF NOT EXISTS ix_kitchen_print_deliveries_unknown
    ON kitchen.physical_print_deliveries (status)
    WHERE status = 'Unknown';
