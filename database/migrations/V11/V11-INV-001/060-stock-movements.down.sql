-- Migration 060 Down: Rollback Immutable Stock Movements Ledger (V11-INV-001)
DROP TRIGGER IF EXISTS trg_stock_movements_immutable ON inventory.stock_movements;
DROP FUNCTION IF EXISTS inventory.prevent_stock_movement_mutation();
DROP TABLE IF EXISTS inventory.stock_movements;
