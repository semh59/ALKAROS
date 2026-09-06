-- Migration 061 Down: Rollback Stock Balances Projection (V11-INV-002)
DROP TABLE IF EXISTS inventory.stock_balances;
