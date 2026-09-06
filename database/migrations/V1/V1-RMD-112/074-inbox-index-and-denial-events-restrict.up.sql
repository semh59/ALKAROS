-- V1-RMD-112: two independently-verified findings from an independent
-- audit (docs/audit/INDEPENDENT_DEEP_AUDIT_2026-09-06.md), 2026-09-06.

-- 1. inbox_messages had NO index supporting its own claim query
--    (WHERE status = 'pending' AND (next_retry_at IS NULL OR next_retry_at
--    <= now()) ORDER BY received_at ... FOR UPDATE SKIP LOCKED,
--    src/BuildingBlocks/Messaging/InboxStore.cs) — every poll did a
--    sequential scan. Partial index scoped to the two claimable statuses
--    only; a fully processed/dead message never needs this index again.
CREATE INDEX IF NOT EXISTS ix_inbox_messages_claimable
    ON inbox_messages (received_at)
    WHERE status IN ('pending', 'in_flight');

-- 2. identity.denial_events (the security denial audit log) cascaded on
--    its user FK — deleting a user silently destroyed every denial record
--    naming them. No code path deletes identity.users today (KVKK
--    retention anonymizes, never deletes — kvkk-retention command), so
--    this was latent, but RESTRICT is the correct invariant: an audit
--    trail must never be an accidental side effect of an unrelated
--    delete, and this forces any future user-deletion path to make an
--    explicit decision about it instead.
ALTER TABLE identity.denial_events
    DROP CONSTRAINT fk_denial_events_user;

ALTER TABLE identity.denial_events
    ADD CONSTRAINT fk_denial_events_user FOREIGN KEY (user_id)
        REFERENCES identity.users (user_id) ON DELETE RESTRICT;
