-- Migration 062 Down: Drop Stock Movement Reversal Unique Constraint (V11-INV-003)
DROP INDEX IF EXISTS inventory.uq_stock_movements_single_reversal;
