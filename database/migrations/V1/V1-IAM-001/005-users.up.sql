CREATE SCHEMA IF NOT EXISTS identity;

CREATE TABLE IF NOT EXISTS identity.users (
    user_id              UUID         NOT NULL,
    username             VARCHAR(100) NOT NULL,
    password_hash        VARCHAR(255) NOT NULL,
    display_name         VARCHAR(200) NOT NULL,
    email                VARCHAR(255) NULL,
    phone                VARCHAR(50)  NULL,
    active               BOOLEAN      NOT NULL DEFAULT TRUE,
    failed_login_attempts INTEGER     NOT NULL DEFAULT 0,
    locked_until         TIMESTAMPTZ  NULL,
    last_login_at        TIMESTAMPTZ  NULL,
    created_at           TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_at           TIMESTAMPTZ  NOT NULL DEFAULT now(),
    row_version          BIGINT       NOT NULL DEFAULT 0,
    PRIMARY KEY (user_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_users_username ON identity.users (username);
CREATE INDEX IF NOT EXISTS ix_users_active ON identity.users (active);
