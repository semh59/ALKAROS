DROP TRIGGER IF EXISTS trg_account_transactions_apply_balance ON customer_account.account_transactions;
DROP FUNCTION IF EXISTS customer_account.apply_transaction_to_balance();
DROP TABLE IF EXISTS customer_account.balance_snapshots;
DROP TABLE IF EXISTS customer_account.balances;
