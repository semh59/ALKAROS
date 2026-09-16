DROP TRIGGER IF EXISTS trg_stock_physical_counts_immutable ON inventory.stock_physical_counts;
DROP FUNCTION IF EXISTS inventory.prevent_physical_count_mutation();
DROP TABLE IF EXISTS inventory.stock_physical_counts;
