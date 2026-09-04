-- Reverse of 051, in reverse order.
ALTER TABLE identity.authorization_delegations
    DROP CONSTRAINT IF EXISTS ck_authorization_delegations_revoked_by;

ALTER TABLE identity.authorization_delegations
    DROP COLUMN IF EXISTS revoked_by_user_id;
