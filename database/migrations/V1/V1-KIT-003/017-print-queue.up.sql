CREATE SCHEMA IF NOT EXISTS kitchen;

CREATE TABLE IF NOT EXISTS kitchen.print_jobs (
    id                UUID         PRIMARY KEY,
    ticket_id         UUID         NOT NULL REFERENCES kitchen.kitchen_tickets(id) ON DELETE RESTRICT,
    printer_id        UUID         NOT NULL REFERENCES kitchen.printers(id) ON DELETE RESTRICT,
    idempotency_key   VARCHAR(128) NOT NULL,
    payload           TEXT         NOT NULL,
    status            VARCHAR(32)  NOT NULL CHECK (status IN ('Pending', 'Leased', 'Printing', 'Printed', 'Failed', 'DeadLetter', 'Cancelled')),
    attempt_count     INT          NOT NULL DEFAULT 0,
    max_attempts      INT          NOT NULL DEFAULT 5,
    next_attempt_at   TIMESTAMPTZ  NULL,
    leased_by         VARCHAR(128) NULL,
    lease_expires_at  TIMESTAMPTZ  NULL,
    printed_at        TIMESTAMPTZ  NULL,
    failed_at         TIMESTAMPTZ  NULL,
    last_error        TEXT         NULL,
    row_version       BIGINT       NOT NULL DEFAULT 1,
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at        TIMESTAMPTZ  NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_kitchen_print_jobs_idempotency_key
    ON kitchen.print_jobs (idempotency_key);

CREATE INDEX IF NOT EXISTS ix_kitchen_print_jobs_ticket_id
    ON kitchen.print_jobs (ticket_id);

CREATE INDEX IF NOT EXISTS ix_kitchen_print_jobs_printer_id
    ON kitchen.print_jobs (printer_id);

CREATE INDEX IF NOT EXISTS ix_kitchen_print_jobs_dispatch
    ON kitchen.print_jobs (status, next_attempt_at, lease_expires_at);
