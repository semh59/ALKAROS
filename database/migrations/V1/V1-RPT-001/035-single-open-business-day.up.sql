CREATE UNIQUE INDEX IF NOT EXISTS ux_reporting_single_open_business_day
    ON reporting.daily_business_days ((1))
    WHERE status = 'Open';
