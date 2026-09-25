-- V12-ONL-002: the outcome of processing each stored webhook event, written in the same
-- transaction as the effect it describes, so an event is processed exactly once. A NULL
-- processed_at means the event still waits for processing. A failing event is retried a
-- bounded number of times (processing_attempts/last_error) and then closed as Failed, so one
-- poison event can never block the events behind it.
ALTER TABLE online_ordering.yemeksepeti_webhook_inbox
    ADD COLUMN IF NOT EXISTS processed_at        TIMESTAMPTZ  NULL,
    ADD COLUMN IF NOT EXISTS processing_outcome  VARCHAR(40)  NULL,
    ADD COLUMN IF NOT EXISTS order_id            UUID         NULL,
    ADD COLUMN IF NOT EXISTS outcome_detail      JSONB        NULL,
    ADD COLUMN IF NOT EXISTS processing_attempts INT          NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS last_error          VARCHAR(200) NULL;

ALTER TABLE online_ordering.yemeksepeti_webhook_inbox
    DROP CONSTRAINT IF EXISTS ck_yemeksepeti_webhook_inbox_processed;
ALTER TABLE online_ordering.yemeksepeti_webhook_inbox
    ADD CONSTRAINT ck_yemeksepeti_webhook_inbox_processed
        CHECK ((processed_at IS NULL) = (processing_outcome IS NULL));

CREATE INDEX IF NOT EXISTS ix_yemeksepeti_webhook_inbox_pending
    ON online_ordering.yemeksepeti_webhook_inbox (processing_attempts, received_at)
    WHERE processed_at IS NULL;
