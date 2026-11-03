-- Any closing difference carries the cashier's explanation, and a reconciliation made by
-- the person who ran or closed the session is marked. Sessions closed before this migration are not re-checked.
ALTER TABLE cash.cash_sessions ADD COLUMN IF NOT EXISTS difference_explanation TEXT NULL;
ALTER TABLE cash.cash_sessions ADD COLUMN IF NOT EXISTS reconciled_self_approved BOOLEAN NOT NULL DEFAULT false;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_cash_sessions_difference_explained') THEN
        ALTER TABLE cash.cash_sessions
            ADD CONSTRAINT ck_cash_sessions_difference_explained
            CHECK (status NOT IN ('Closed', 'Reconciled') OR difference = 0
                   OR (difference_explanation IS NOT NULL AND char_length(btrim(difference_explanation)) BETWEEN 1 AND 500))
            NOT VALID;
    END IF;
END $$;
