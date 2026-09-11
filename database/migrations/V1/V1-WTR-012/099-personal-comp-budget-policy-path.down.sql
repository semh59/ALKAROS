-- Reverse of 099.
-- NOTE: this fails if any row already carries policy_path = 'personal_budget'
-- (the same DROP-then-ADD-narrower pattern every other CHECK-widening
-- migration in this tree uses — a down migration is a rollback of a
-- deployment that has not yet accepted live personal-budget grants, not a
-- data-destructive tool).
ALTER TABLE identity.authorization_grants
    DROP CONSTRAINT ck_authorization_grants_policy_path;

ALTER TABLE identity.authorization_grants
    ADD CONSTRAINT ck_authorization_grants_policy_path
        CHECK (policy_path IS NULL OR policy_path IN ('auto', 'delegation', 'manual'));
