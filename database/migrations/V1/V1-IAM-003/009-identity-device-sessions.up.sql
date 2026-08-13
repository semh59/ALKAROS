CREATE SCHEMA IF NOT EXISTS identity;

CREATE TABLE IF NOT EXISTS identity.device_sessions (
    session_id   UUID         NOT NULL,
    user_id      UUID         NOT NULL,
    device_id    VARCHAR(255) NOT NULL,
    token_hash   VARCHAR(64)  NOT NULL,
    created_at   TIMESTAMPTZ  NOT NULL DEFAULT now(),
    expires_at   TIMESTAMPTZ  NOT NULL,
    revoked_at   TIMESTAMPTZ  NULL,
    last_seen_at TIMESTAMPTZ  NULL,
    PRIMARY KEY (session_id),
    UNIQUE (token_hash),
    CONSTRAINT fk_device_sessions_user FOREIGN KEY (user_id)
        REFERENCES identity.users (user_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_device_sessions_user ON identity.device_sessions (user_id, device_id);

CREATE TABLE IF NOT EXISTS identity.session_operations (
    operation_id  UUID        NOT NULL,
    session_id    UUID        NOT NULL,
    queued_at     TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (operation_id),
    CONSTRAINT fk_session_operations_session FOREIGN KEY (session_id)
        REFERENCES identity.device_sessions (session_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_session_operations_session ON identity.session_operations (session_id);