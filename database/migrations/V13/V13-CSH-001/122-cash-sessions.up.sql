-- V13-CSH-001: CashSession lifecycle persistence (V1-CSH-001 design,
-- PDF:I.38-I.44, PDF:II.2.7, PDF:II.5.9, PDF:III.9).
CREATE SCHEMA IF NOT EXISTS cash;

CREATE TABLE IF NOT EXISTS cash.cash_sessions (
    cash_session_id        UUID          NOT NULL,
    cashier_user_id        UUID          NOT NULL,
    terminal_id             UUID          NOT NULL,
    status                  TEXT          NOT NULL CHECK (status IN ('Open', 'Counting', 'Closing', 'Closed', 'Reconciled')),
    opening_balance          NUMERIC(18,2) NOT NULL CHECK (opening_balance >= 0),
    expected_cash            NUMERIC(18,2) NOT NULL DEFAULT 0,
    actual_cash              NUMERIC(18,2) NOT NULL DEFAULT 0 CHECK (actual_cash >= 0),
    difference               NUMERIC(18,2) NOT NULL DEFAULT 0,
    opened_at                TIMESTAMPTZ   NOT NULL,
    closed_at                TIMESTAMPTZ   NULL,
    closed_by                UUID          NULL,
    is_supervisor_override   BOOLEAN       NOT NULL DEFAULT false,
    override_reason          TEXT          NULL,
    reconciled_at            TIMESTAMPTZ   NULL,
    reconciled_by            UUID          NULL,
    reconciliation_notes     TEXT          NULL,
    row_version              BIGINT        NOT NULL DEFAULT 1,
    created_at               TIMESTAMPTZ   NOT NULL,
    updated_at               TIMESTAMPTZ   NOT NULL,
    PRIMARY KEY (cash_session_id),
    CONSTRAINT ck_cash_sessions_override_reason_required
        CHECK (NOT is_supervisor_override OR override_reason IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS ix_cash_sessions_terminal ON cash.cash_sessions (terminal_id);

-- CSH-INV-01 (docs/domain/cash-session-design.md §2.1): a terminal may have
-- at most one active (Open/Counting/Closing) session at any moment,
-- enforced here in addition to the application-level policy check so a
-- future caller that bypasses the service cannot silently violate it.
CREATE UNIQUE INDEX IF NOT EXISTS ux_cash_sessions_one_active_per_terminal
    ON cash.cash_sessions (terminal_id)
    WHERE status IN ('Open', 'Counting', 'Closing');

CREATE TABLE IF NOT EXISTS cash.cash_counts (
    cash_count_id      UUID          NOT NULL,
    cash_session_id    UUID          NOT NULL,
    counted_amount     NUMERIC(18,2) NOT NULL CHECK (counted_amount >= 0),
    counted_by         UUID          NOT NULL,
    notes              TEXT          NULL,
    counted_at         TIMESTAMPTZ   NOT NULL,
    PRIMARY KEY (cash_count_id),
    CONSTRAINT fk_cash_counts_session FOREIGN KEY (cash_session_id)
        REFERENCES cash.cash_sessions (cash_session_id)
);

CREATE INDEX IF NOT EXISTS ix_cash_counts_session ON cash.cash_counts (cash_session_id);
