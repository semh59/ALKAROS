-- Reverse of 195: restores the original grant transition function from 045.
DROP TRIGGER IF EXISTS trg_authorization_delegations_transition ON identity.authorization_delegations;
DROP FUNCTION IF EXISTS identity.enforce_authorization_delegation_transition();

ALTER TABLE identity.authorization_grants
    DROP CONSTRAINT IF EXISTS ck_authorization_grants_approver_not_requester;

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
