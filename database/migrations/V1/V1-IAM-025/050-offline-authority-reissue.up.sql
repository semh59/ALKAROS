-- V1-IAM-025 (A1, docs/engineering/authz-wave-remediation-plan.md Phase 3):
-- offline budget re-issue no longer deletes the prior session's row first
-- (see PostgresOfflineAuthorityBudgetRepository.CreateAsync). A reconciled
-- prior budget can already have identity.offline_authority_replays rows, and
-- that table's budget_id FK has no ON DELETE CASCADE, so the delete would
-- throw exactly when a device most needs a fresh budget. Dropping the
-- per-session uniqueness lets re-issue simply insert a new row; the
-- repository's session lookup answers with the most recently issued one
-- (ORDER BY issued_at DESC LIMIT 1).
ALTER TABLE identity.offline_authority_budgets
    DROP CONSTRAINT uq_offline_authority_budgets_session;
