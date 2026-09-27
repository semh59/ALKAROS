ALTER TABLE identity.denial_events
    DROP CONSTRAINT IF EXISTS fk_denial_events_user;

ALTER TABLE identity.denial_events
    ADD CONSTRAINT fk_denial_events_user FOREIGN KEY (user_id)
        REFERENCES identity.users (user_id) ON DELETE CASCADE;

DROP INDEX IF EXISTS ix_inbox_messages_claimable;
