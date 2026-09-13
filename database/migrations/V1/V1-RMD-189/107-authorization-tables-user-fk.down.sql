ALTER TABLE identity.authorization_policies
    DROP CONSTRAINT IF EXISTS fk_authorization_policies_updated_by;

ALTER TABLE identity.behavioural_tightenings
    DROP CONSTRAINT IF EXISTS fk_behavioural_tightenings_cleared_by,
    DROP CONSTRAINT IF EXISTS fk_behavioural_tightenings_user;

ALTER TABLE identity.authorization_delegations
    DROP CONSTRAINT IF EXISTS fk_authorization_delegations_delegator,
    DROP CONSTRAINT IF EXISTS fk_authorization_delegations_grantee;

ALTER TABLE identity.authorization_grants
    DROP CONSTRAINT IF EXISTS fk_authorization_grants_subject_serving_user,
    DROP CONSTRAINT IF EXISTS fk_authorization_grants_approver,
    DROP CONSTRAINT IF EXISTS fk_authorization_grants_requester;
