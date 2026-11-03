ALTER TABLE cash.cash_sessions DROP CONSTRAINT IF EXISTS ck_cash_sessions_difference_explained;
ALTER TABLE cash.cash_sessions DROP COLUMN IF EXISTS reconciled_self_approved;
ALTER TABLE cash.cash_sessions DROP COLUMN IF EXISTS difference_explanation;
