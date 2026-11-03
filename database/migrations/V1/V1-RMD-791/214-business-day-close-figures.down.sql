ALTER TABLE reporting.daily_business_days DROP COLUMN IF EXISTS cash_reconciliation;
ALTER TABLE reporting.daily_business_days DROP COLUMN IF EXISTS table_breakdown;
ALTER TABLE reporting.daily_business_days DROP COLUMN IF EXISTS open_items_acknowledged;
ALTER TABLE reporting.daily_business_days DROP COLUMN IF EXISTS unsettled_payments_count;
ALTER TABLE reporting.daily_business_days DROP COLUMN IF EXISTS open_bills_count;
ALTER TABLE reporting.daily_business_days DROP COLUMN IF EXISTS open_cash_sessions_count;
ALTER TABLE reporting.daily_business_days DROP COLUMN IF EXISTS total_tips_amount;
