CREATE INDEX IF NOT EXISTS ix_balance_snapshots_date
    ON customer_account.balance_snapshots (snapshot_date);
