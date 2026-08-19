ALTER TABLE inbox_messages
    ADD COLUMN lease_generation BIGINT NOT NULL DEFAULT 0;

ALTER TABLE outbox_messages
    ADD COLUMN lease_generation BIGINT NOT NULL DEFAULT 0;
