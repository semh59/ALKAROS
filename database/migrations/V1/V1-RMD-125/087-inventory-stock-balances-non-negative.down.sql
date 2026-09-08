DROP TRIGGER IF EXISTS trg_stock_balances_non_negative ON inventory.stock_balances;
DROP FUNCTION IF EXISTS inventory.prevent_negative_on_hand_balance();
