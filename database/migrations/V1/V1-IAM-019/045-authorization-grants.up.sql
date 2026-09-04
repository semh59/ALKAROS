-- V1-IAM-019: the asynchronous grant request/resolution log
-- (docs/domain/authorization-model.md §4). One row per protected-action instance
-- that could not be authorized outright: inserted 'pending', then resolved
-- exactly once to 'granted' or 'denied'. Every terminal row carries the
-- policy_path (auto | delegation | manual), the reason_code and the monetary
-- delta, so reporting gets "comp/void/discount by day, reason and role" for free.

CREATE TABLE identity.authorization_grants (
    grant_id                UUID          NOT NULL DEFAULT gen_random_uuid(),
    idempotency_key         TEXT          NOT NULL,
    permission_code         TEXT          NOT NULL,
    requester_user_id       UUID          NOT NULL,
    requester_role_code     TEXT          NOT NULL,
    subject_type            TEXT,
    subject_id              UUID,
    subject_serving_user_id UUID,
    amount                  NUMERIC(12, 2) NOT NULL DEFAULT 0,
    reason_code             TEXT          NOT NULL,
    requested_at            TIMESTAMPTZ   NOT NULL DEFAULT now(),
    status                  TEXT          NOT NULL DEFAULT 'pending',
    policy_path             TEXT,
    approver_user_id        UUID,
    resolved_at             TIMESTAMPTZ,
    PRIMARY KEY (grant_id),
    CONSTRAINT uq_authorization_grants_idempotency UNIQUE (idempotency_key),
    CONSTRAINT ck_authorization_grants_status
        CHECK (status IN ('pending', 'granted', 'denied')),
    CONSTRAINT ck_authorization_grants_policy_path
        CHECK (policy_path IS NULL OR policy_path IN ('auto', 'delegation', 'manual')),
    CONSTRAINT ck_authorization_grants_resolution
        CHECK ((status = 'pending') = (resolved_at IS NULL)
               AND (status = 'pending' OR policy_path IS NOT NULL)),
    CONSTRAINT ck_authorization_grants_amount CHECK (amount >= 0),
    CONSTRAINT ck_authorization_grants_subject
        CHECK ((subject_type IS NULL) = (subject_id IS NULL))
);

CREATE INDEX ix_authorization_grants_pending
    ON identity.authorization_grants (requested_at)
    WHERE status = 'pending';

CREATE INDEX ix_authorization_grants_auto_window
    ON identity.authorization_grants (requester_user_id, permission_code, resolved_at)
    WHERE status = 'granted' AND policy_path = 'auto';

-- Append-once: a row is born 'pending' and updated exactly once to a terminal
-- status. No un-resolve, no re-resolve, no delete, no mutation of the request
-- fields. Fails closed at the engine level.
CREATE OR REPLACE FUNCTION identity.enforce_authorization_grant_transition()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'identity.authorization_grants rows cannot be deleted.';
    END IF;
    IF OLD.status <> 'pending' THEN
        RAISE EXCEPTION 'authorization grant % is already resolved (%).', OLD.grant_id, OLD.status;
    END IF;
    IF NEW.status NOT IN ('granted', 'denied') THEN
        RAISE EXCEPTION 'a pending authorization grant may only move to granted or denied.';
    END IF;
    IF NEW.grant_id <> OLD.grant_id
       OR NEW.idempotency_key <> OLD.idempotency_key
       OR NEW.permission_code <> OLD.permission_code
       OR NEW.requester_user_id <> OLD.requester_user_id
       OR NEW.amount <> OLD.amount
       OR NEW.reason_code <> OLD.reason_code
       OR NEW.requested_at <> OLD.requested_at THEN
        RAISE EXCEPTION 'only the resolution fields of an authorization grant may change.';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_authorization_grants_transition
BEFORE UPDATE OR DELETE ON identity.authorization_grants
FOR EACH ROW EXECUTE FUNCTION identity.enforce_authorization_grant_transition();

-- Reporting projection: granted authorizations rolled up by UTC day, permission,
-- reason, requester role and policy path. The reporting schema is created by
-- migration 031 (V1-RPT-001); the guard here only matters when this migration
-- is applied in isolation (tests).
CREATE SCHEMA IF NOT EXISTS reporting;

CREATE VIEW reporting.authorization_grant_daily AS
SELECT (resolved_at AT TIME ZONE 'UTC')::date AS grant_date,
       permission_code,
       reason_code,
       requester_role_code,
       policy_path,
       count(*)    AS grant_count,
       sum(amount) AS amount_total
FROM identity.authorization_grants
WHERE status = 'granted'
GROUP BY 1, 2, 3, 4, 5;
