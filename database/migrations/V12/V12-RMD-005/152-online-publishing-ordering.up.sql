-- V12-RMD-005: availability observations get their version from one sequence drawn under a per-channel lock,
-- so every refresh is newer than the last even when only a mapping changed; a failed availability row waits
-- before its next attempt; a catalog publication overtaken by a newer one is marked Superseded, not sent.
CREATE SEQUENCE IF NOT EXISTS online_ordering.availability_observation_seq;
DO $$ BEGIN
    IF to_regclass('online_ordering.availability_states') IS NOT NULL THEN
        PERFORM setval('online_ordering.availability_observation_seq',
                       COALESCE((SELECT max(desired_version) FROM online_ordering.availability_states), 0) + 1, false);
    END IF;
END $$;

ALTER TABLE IF EXISTS online_ordering.availability_states
    ADD COLUMN IF NOT EXISTS next_attempt_at TIMESTAMPTZ NULL;

ALTER TABLE IF EXISTS online_ordering.catalog_publications DROP CONSTRAINT IF EXISTS catalog_publications_status_check;
ALTER TABLE IF EXISTS online_ordering.catalog_publications
    ADD CONSTRAINT catalog_publications_status_check
        CHECK (status IN ('Pending', 'Delivered', 'Unchanged', 'NothingToPublish', 'Superseded'));
