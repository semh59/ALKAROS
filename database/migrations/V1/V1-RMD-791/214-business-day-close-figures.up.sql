-- A business-day close stores what the server counted: tips as a separate line (outside revenue), the open items the
-- manager acknowledged, the per-table breakdown and the cash reconciliation totals of the day.
ALTER TABLE reporting.daily_business_days ADD COLUMN IF NOT EXISTS total_tips_amount NUMERIC(12,2) NOT NULL DEFAULT 0.00;
ALTER TABLE reporting.daily_business_days ADD COLUMN IF NOT EXISTS open_cash_sessions_count INT NOT NULL DEFAULT 0;
ALTER TABLE reporting.daily_business_days ADD COLUMN IF NOT EXISTS open_bills_count INT NOT NULL DEFAULT 0;
ALTER TABLE reporting.daily_business_days ADD COLUMN IF NOT EXISTS unsettled_payments_count INT NOT NULL DEFAULT 0;
ALTER TABLE reporting.daily_business_days ADD COLUMN IF NOT EXISTS open_items_acknowledged BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE reporting.daily_business_days ADD COLUMN IF NOT EXISTS table_breakdown JSONB NOT NULL DEFAULT '[]'::jsonb;
ALTER TABLE reporting.daily_business_days ADD COLUMN IF NOT EXISTS cash_reconciliation JSONB NULL;
