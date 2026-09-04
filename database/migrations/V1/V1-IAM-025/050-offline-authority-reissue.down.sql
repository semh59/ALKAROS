-- Reverse of 050. Re-adding the per-session uniqueness only succeeds if no
-- session has more than one budget row left -- true immediately after 050's
-- up in the migration-chain tests, but not guaranteed once re-issues have
-- actually run against a live database. That asymmetry is intentional: 050's
-- whole point is that a session legitimately accumulates more than one
-- budget row over time, so this down is a schema-test round-trip, not a safe
-- production rollback once the up side has been live.
ALTER TABLE identity.offline_authority_budgets
    ADD CONSTRAINT uq_offline_authority_budgets_session UNIQUE (session_id);
