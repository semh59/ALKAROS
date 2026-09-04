-- V1-IAM-021: time-boxed delegation (docs/domain/authorization-model.md §4
-- step 2). A manager hands a bounded slice of a permission to another user for a
-- fixed window: "Ayse holds bills.comp up to 200 TRY until 22:00". The grant flow
-- consults this table between the policy engine and a manager decision.
--
-- Expiry is automatic — a row stops covering grants the moment expires_at is
-- reached, enforced by the query filter, so no sweeper job is needed. revoked_at
-- records an early cancellation by the delegator.

CREATE TABLE identity.authorization_delegations (
    delegation_id      UUID          NOT NULL DEFAULT gen_random_uuid(),
    permission_code    TEXT          NOT NULL,
    grantee_user_id    UUID          NOT NULL,
    delegator_user_id  UUID          NOT NULL,
    limit_amount       NUMERIC(12, 2) NOT NULL,
    granted_at         TIMESTAMPTZ   NOT NULL DEFAULT now(),
    expires_at         TIMESTAMPTZ   NOT NULL,
    revoked_at         TIMESTAMPTZ,
    PRIMARY KEY (delegation_id),
    CONSTRAINT ck_authorization_delegations_window CHECK (expires_at > granted_at),
    CONSTRAINT ck_authorization_delegations_limit CHECK (limit_amount >= 0),
    CONSTRAINT ck_authorization_delegations_revoked CHECK (revoked_at IS NULL OR revoked_at >= granted_at),
    CONSTRAINT ck_authorization_delegations_self CHECK (grantee_user_id <> delegator_user_id)
);

CREATE INDEX ix_authorization_delegations_active
    ON identity.authorization_delegations (grantee_user_id, permission_code, expires_at)
    WHERE revoked_at IS NULL;
