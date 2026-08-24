CREATE SCHEMA IF NOT EXISTS customer_display;

CREATE TABLE customer_display.terminals (
    terminal_id      UUID        NOT NULL,
    active_order_id  UUID        NULL,
    row_version      BIGINT      NOT NULL DEFAULT 1 CHECK (row_version > 0),
    created_at       TIMESTAMPTZ NOT NULL,
    updated_at       TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (terminal_id),
    CONSTRAINT fk_customer_display_terminal_order
        FOREIGN KEY (active_order_id) REFERENCES orders.orders(order_id)
);

CREATE TABLE customer_display.pairing_requests (
    request_id           UUID        NOT NULL,
    display_id           UUID        NOT NULL,
    terminal_id          UUID        NULL,
    pairing_secret_hash  CHAR(64)    NOT NULL,
    code_hash            CHAR(64)    NOT NULL,
    expires_at           TIMESTAMPTZ NOT NULL,
    approved_at          TIMESTAMPTZ NULL,
    consumed_at          TIMESTAMPTZ NULL,
    failed_attempts      SMALLINT    NOT NULL DEFAULT 0 CHECK (failed_attempts BETWEEN 0 AND 5),
    created_at           TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (request_id),
    CONSTRAINT fk_customer_display_pairing_terminal
        FOREIGN KEY (terminal_id) REFERENCES customer_display.terminals(terminal_id)
);

CREATE UNIQUE INDEX ux_customer_display_open_pairing
    ON customer_display.pairing_requests(display_id)
    WHERE consumed_at IS NULL;

CREATE UNIQUE INDEX ux_customer_display_pairing_code
    ON customer_display.pairing_requests(code_hash)
    WHERE consumed_at IS NULL;

CREATE TABLE customer_display.display_sessions (
    session_id   UUID        NOT NULL,
    display_id   UUID        NOT NULL,
    terminal_id  UUID        NOT NULL,
    token_hash   CHAR(64)    NOT NULL,
    created_at   TIMESTAMPTZ NOT NULL,
    expires_at   TIMESTAMPTZ NOT NULL,
    revoked_at   TIMESTAMPTZ NULL,
    last_seen_at TIMESTAMPTZ NULL,
    PRIMARY KEY (session_id),
    UNIQUE (token_hash),
    CONSTRAINT fk_customer_display_session_terminal
        FOREIGN KEY (terminal_id) REFERENCES customer_display.terminals(terminal_id)
);

CREATE UNIQUE INDEX ux_customer_display_active_session
    ON customer_display.display_sessions(display_id)
    WHERE revoked_at IS NULL;

CREATE INDEX ix_customer_display_session_terminal
    ON customer_display.display_sessions(terminal_id)
    WHERE revoked_at IS NULL;
