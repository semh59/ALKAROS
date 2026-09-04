-- V1-IAM-025 (D4, docs/engineering/authz-wave-remediation-plan.md Phase 3): a
-- delegation revoke records who revoked it, matching the audit completeness
-- already present on behavioural_tightenings.cleared_by_user_id and
-- authorization_grants.approver_user_id (model §8, "one audit row per
-- action"). Existing rows have no actor on file, so the column stays
-- nullable; the CHECK keeps it in lockstep with revoked_at going forward.
ALTER TABLE identity.authorization_delegations
    ADD COLUMN revoked_by_user_id UUID;

ALTER TABLE identity.authorization_delegations
    ADD CONSTRAINT ck_authorization_delegations_revoked_by
    CHECK ((revoked_at IS NULL) = (revoked_by_user_id IS NULL));
