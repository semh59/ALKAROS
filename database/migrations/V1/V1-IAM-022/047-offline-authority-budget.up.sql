-- V1-IAM-022: bounded offline authority (docs/domain/authorization-model.md §5).
--
-- At session start the Host issues the device a short-lived offline authority
-- budget: a per-permission slice of the requester's own online auto_within
-- policy, snapshotted once and expiring in a few hours. While offline a
-- grant-class action is allowed only while that budget has headroom; on
-- reconnect every offline-authorized action is written to identity.authorization_grants
-- as usual and linked here with identity.offline_authority_replays, so a manager
-- reviews it (offline_pending_review = a pending grant that has a replay row)
-- and reporting sees it with no extra plumbing.
--
-- The budget is a server-held authority row (not a signed client token): the
-- device caches its own copy for offline UX, but reconciliation trusts only the
-- row written here, so the offline headroom cannot be forged. See the task's
-- "Design note" for why this replaces the model's JWT-style sketch.

CREATE TABLE identity.offline_authority_budgets (
    budget_id   UUID        NOT NULL DEFAULT gen_random_uuid(),
    user_id     UUID        NOT NULL,
    session_id  UUID        NOT NULL,
    issued_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at  TIMESTAMPTZ NOT NULL,
    PRIMARY KEY (budget_id),
    CONSTRAINT uq_offline_authority_budgets_session UNIQUE (session_id),
    CONSTRAINT ck_offline_authority_budgets_window CHECK (expires_at > issued_at)
);

CREATE INDEX ix_offline_authority_budgets_expiry
    ON identity.offline_authority_budgets (expires_at);

-- One line per permission the device may self-approve offline. max_count is the
-- number of offline grants allowed in the budget's lifetime; limit_amount caps
-- the monetary delta of each (null = no monetary cap, count only).
CREATE TABLE identity.offline_authority_budget_lines (
    budget_id       UUID          NOT NULL,
    permission_code TEXT          NOT NULL,
    limit_amount    NUMERIC(12, 2),
    max_count       INTEGER       NOT NULL,
    PRIMARY KEY (budget_id, permission_code),
    CONSTRAINT fk_offline_authority_budget_lines_budget
        FOREIGN KEY (budget_id) REFERENCES identity.offline_authority_budgets (budget_id)
        ON DELETE CASCADE,
    CONSTRAINT ck_offline_authority_budget_lines_count CHECK (max_count >= 0),
    CONSTRAINT ck_offline_authority_budget_lines_amount
        CHECK (limit_amount IS NULL OR limit_amount >= 0)
);

-- Links a reconnected offline-authorized grant back to the budget it was spent
-- against. Presence of a row is the "offline_pending_review" flag; the unique
-- grant_id keeps reconciliation idempotent alongside the grant's own
-- idempotency key.
CREATE TABLE identity.offline_authority_replays (
    replay_id             UUID        NOT NULL DEFAULT gen_random_uuid(),
    grant_id              UUID        NOT NULL,
    budget_id             UUID        NOT NULL,
    offline_authorized_at TIMESTAMPTZ NOT NULL,
    reconciled_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (replay_id),
    CONSTRAINT uq_offline_authority_replays_grant UNIQUE (grant_id),
    CONSTRAINT fk_offline_authority_replays_grant
        FOREIGN KEY (grant_id) REFERENCES identity.authorization_grants (grant_id),
    CONSTRAINT fk_offline_authority_replays_budget
        FOREIGN KEY (budget_id) REFERENCES identity.offline_authority_budgets (budget_id),
    CONSTRAINT ck_offline_authority_replays_order
        CHECK (offline_authorized_at <= reconciled_at)
);

CREATE INDEX ix_offline_authority_replays_budget
    ON identity.offline_authority_replays (budget_id);
