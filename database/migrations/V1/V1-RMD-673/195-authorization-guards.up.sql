-- V1-RMD-673: engine-level guards for the authorization trail.
-- Grants: whoever resolves a grant is never its requester, and the role and subject of the request are frozen with the rest.
-- Delegations: never deleted; only the early-revocation fields may change, once.

ALTER TABLE identity.authorization_grants
    ADD CONSTRAINT ck_authorization_grants_approver_not_requester
    CHECK (approver_user_id IS NULL OR approver_user_id <> requester_user_id) NOT VALID;

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
    IF NEW.grant_id IS DISTINCT FROM OLD.grant_id
       OR NEW.idempotency_key IS DISTINCT FROM OLD.idempotency_key
       OR NEW.permission_code IS DISTINCT FROM OLD.permission_code
       OR NEW.requester_user_id IS DISTINCT FROM OLD.requester_user_id
       OR NEW.requester_role_code IS DISTINCT FROM OLD.requester_role_code
       OR NEW.subject_type IS DISTINCT FROM OLD.subject_type
       OR NEW.subject_id IS DISTINCT FROM OLD.subject_id
       OR NEW.subject_serving_user_id IS DISTINCT FROM OLD.subject_serving_user_id
       OR NEW.amount IS DISTINCT FROM OLD.amount
       OR NEW.reason_code IS DISTINCT FROM OLD.reason_code
       OR NEW.requested_at IS DISTINCT FROM OLD.requested_at THEN
        RAISE EXCEPTION 'only the resolution fields of an authorization grant may change.';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION identity.enforce_authorization_delegation_transition()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'identity.authorization_delegations rows cannot be deleted.';
    END IF;
    IF OLD.revoked_at IS NOT NULL THEN
        RAISE EXCEPTION 'authorization delegation % is already revoked.', OLD.delegation_id;
    END IF;
    IF NEW.delegation_id IS DISTINCT FROM OLD.delegation_id
       OR NEW.permission_code IS DISTINCT FROM OLD.permission_code
       OR NEW.grantee_user_id IS DISTINCT FROM OLD.grantee_user_id
       OR NEW.delegator_user_id IS DISTINCT FROM OLD.delegator_user_id
       OR NEW.limit_amount IS DISTINCT FROM OLD.limit_amount
       OR NEW.granted_at IS DISTINCT FROM OLD.granted_at
       OR NEW.expires_at IS DISTINCT FROM OLD.expires_at THEN
        RAISE EXCEPTION 'only the revocation fields of an authorization delegation may change.';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_authorization_delegations_transition
BEFORE UPDATE OR DELETE ON identity.authorization_delegations
FOR EACH ROW EXECUTE FUNCTION identity.enforce_authorization_delegation_transition();
