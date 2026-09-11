-- V1-WTR-012: personal daily comp (bills.comp) allowance (garson audit
-- follow-on feature ideation, 2026-09-11). A role that does not hold
-- bills.comp outright (only 'waiter') used to have every comp request go to
-- a manager, no matter how small. A new PolicyPath.PersonalBudget resolver
-- (PersonalCompBudgetEscalationResolver, added to the IEscalationResolver
-- chain after DelegationEscalationResolver) now lets the waiter role
-- self-approve within a per-item and per-day cap.
--
-- No separate budget/ledger table is needed: every self-approved comp is
-- already written to identity.authorization_grants as a normal 'granted'
-- row, the same way Auto/Delegation/Manual rows are. The amount already
-- spent today is computed by summing this table filtered to
-- requester_user_id + permission_code + policy_path = 'personal_budget' +
-- today's UTC day — the (requester_user_id, permission_code, resolved_at)
-- WHERE status = 'granted' partial index added by migration 052
-- (ix_authorization_grants_granted_rate) already covers this query shape,
-- so no new index is added here.
ALTER TABLE identity.authorization_grants
    DROP CONSTRAINT ck_authorization_grants_policy_path;

ALTER TABLE identity.authorization_grants
    ADD CONSTRAINT ck_authorization_grants_policy_path
        CHECK (policy_path IS NULL OR policy_path IN ('auto', 'delegation', 'manual', 'personal_budget'));
