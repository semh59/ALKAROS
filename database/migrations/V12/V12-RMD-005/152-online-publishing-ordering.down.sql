-- V12-RMD-005: back to the V12-ONL-004/005 shape. A Superseded publication (never sent) becomes Unchanged,
-- the closest earlier status that also means "not sent".
DO $$ BEGIN
    IF to_regclass('online_ordering.catalog_publications') IS NOT NULL THEN
        UPDATE online_ordering.catalog_publications SET status = 'Unchanged' WHERE status = 'Superseded';
    END IF;
END $$;
ALTER TABLE IF EXISTS online_ordering.catalog_publications DROP CONSTRAINT IF EXISTS catalog_publications_status_check;
ALTER TABLE IF EXISTS online_ordering.catalog_publications
    ADD CONSTRAINT catalog_publications_status_check
        CHECK (status IN ('Pending', 'Delivered', 'Unchanged', 'NothingToPublish'));
ALTER TABLE IF EXISTS online_ordering.availability_states DROP COLUMN IF EXISTS next_attempt_at;
DROP SEQUENCE IF EXISTS online_ordering.availability_observation_seq;
