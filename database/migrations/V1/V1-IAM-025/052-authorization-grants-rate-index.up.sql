-- V1-IAM-025 (D5, docs/engineering/authz-wave-remediation-plan.md Phase 3):
-- PostgresBehaviouralRateSource.CountGrantedSinceAsync filters
-- (requester_user_id, permission_code, status = 'granted', resolved_at) with
-- no covering index -- ix_authorization_grants_auto_window (migration 045) is
-- partial to policy_path = 'auto', but the behavioural rate counts every
-- granted row regardless of path, so every gate check was a sequential scan.
CREATE INDEX ix_authorization_grants_granted_rate
    ON identity.authorization_grants (requester_user_id, permission_code, resolved_at)
    WHERE status = 'granted';
