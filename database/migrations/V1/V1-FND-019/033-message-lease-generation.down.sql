ALTER TABLE outbox_messages
    DROP COLUMN IF EXISTS lease_generation;

ALTER TABLE inbox_messages
    DROP COLUMN IF EXISTS lease_generation;
