CREATE SCHEMA IF NOT EXISTS identity;

CREATE TABLE IF NOT EXISTS identity.permissions (
    permission_id   UUID         NOT NULL,
    code            VARCHAR(100) NOT NULL,
    name            VARCHAR(200) NOT NULL,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT now(),
    PRIMARY KEY (permission_id),
    UNIQUE (code)
);

CREATE TABLE IF NOT EXISTS identity.roles (
    role_id     UUID         NOT NULL,
    code        VARCHAR(100) NOT NULL,
    name        VARCHAR(200) NOT NULL,
    created_at  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    PRIMARY KEY (role_id),
    UNIQUE (code)
);

CREATE TABLE IF NOT EXISTS identity.role_permissions (
    role_permission_id  UUID    NOT NULL,
    role_id             UUID    NOT NULL,
    permission_id       UUID    NOT NULL,
    PRIMARY KEY (role_permission_id),
    UNIQUE (role_id, permission_id),
    CONSTRAINT fk_role_permissions_role FOREIGN KEY (role_id)
        REFERENCES identity.roles (role_id) ON DELETE CASCADE,
    CONSTRAINT fk_role_permissions_permission FOREIGN KEY (permission_id)
        REFERENCES identity.permissions (permission_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_role_permissions_permission ON identity.role_permissions (permission_id);

CREATE TABLE IF NOT EXISTS identity.user_roles (
    user_role_id UUID NOT NULL,
    user_id      UUID NOT NULL,
    role_id      UUID NOT NULL,
    PRIMARY KEY (user_role_id),
    UNIQUE (user_id, role_id),
    CONSTRAINT fk_user_roles_user FOREIGN KEY (user_id)
        REFERENCES identity.users (user_id) ON DELETE CASCADE,
    CONSTRAINT fk_user_roles_role FOREIGN KEY (role_id)
        REFERENCES identity.roles (role_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_user_roles_role ON identity.user_roles (role_id);

CREATE TABLE IF NOT EXISTS identity.denial_events (
    denial_event_id UUID         NOT NULL,
    user_id         UUID         NULL,
    permission_code VARCHAR(100) NOT NULL,
    reason          VARCHAR(500) NOT NULL,
    occurred_at     TIMESTAMPTZ  NOT NULL DEFAULT now(),
    PRIMARY KEY (denial_event_id),
    CONSTRAINT fk_denial_events_user FOREIGN KEY (user_id)
        REFERENCES identity.users (user_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_denial_events_user ON identity.denial_events (user_id, occurred_at);

INSERT INTO identity.permissions (permission_id, code, name) VALUES
    ('20000000-0000-0000-0000-000000000001', 'identity.users.manage',
     'Manage user accounts and credentials'),
    ('20000000-0000-0000-0000-000000000002', 'identity.roles.manage',
     'Manage roles and role assignments'),
    ('20000000-0000-0000-0000-000000000003', 'identity.permissions.manage',
     'Manage the permission catalog'),
    ('20000000-0000-0000-0000-000000000004', 'identity.device_sessions.manage',
     'Manage device sessions and revocations')
ON CONFLICT (code) DO NOTHING;