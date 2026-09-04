-- V1-IAM-018: the authorization policy engine's storage
-- (docs/domain/authorization-model.md §4 step 1). A policy row narrows how a
-- `grant`-class action for one (permission_code, role_code) pair is resolved:
--
--   always_deny   - the request is refused before it reaches a manager
--   always_allow  - the request is auto-approved unconditionally
--   auto_within   - auto-approve while the monetary delta is <= limit_amount
--                   and the requester has fewer than max_count auto-grants in
--                   the trailing window_seconds; otherwise escalate
--
-- No row for a pair means "escalate to a manager" (the fail-closed default), so
-- the table ships empty and managers populate it from the admin surface.

CREATE TABLE identity.authorization_policies (
    policy_id       UUID        NOT NULL DEFAULT gen_random_uuid(),
    permission_code TEXT        NOT NULL,
    role_code       TEXT        NOT NULL,
    mode            TEXT        NOT NULL,
    limit_amount    NUMERIC(12, 2),
    max_count       INTEGER,
    window_seconds  INTEGER,
    row_version     BIGINT      NOT NULL DEFAULT 1,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by      UUID,
    PRIMARY KEY (policy_id),
    CONSTRAINT uq_authorization_policies_scope UNIQUE (permission_code, role_code),
    CONSTRAINT ck_authorization_policies_mode
        CHECK (mode IN ('always_deny', 'always_allow', 'auto_within')),
    CONSTRAINT ck_authorization_policies_auto_within
        CHECK (mode <> 'auto_within'
               OR (limit_amount IS NOT NULL AND max_count IS NOT NULL AND window_seconds IS NOT NULL)),
    CONSTRAINT ck_authorization_policies_bounds
        CHECK ((limit_amount IS NULL OR limit_amount >= 0)
               AND (max_count IS NULL OR max_count >= 0)
               AND (window_seconds IS NULL OR window_seconds > 0)),
    CONSTRAINT ck_authorization_policies_row_version CHECK (row_version >= 1)
);

CREATE INDEX ix_authorization_policies_permission
    ON identity.authorization_policies (permission_code);
