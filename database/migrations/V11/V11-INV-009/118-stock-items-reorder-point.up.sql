-- Migration 118: StockItem.ReorderPoint (V11-INV-009)
-- Null means no threshold configured yet — existing CriticalStockReport
-- behavior (caller-supplied threshold, default 0) is unchanged.

ALTER TABLE inventory.stock_items
    ADD COLUMN IF NOT EXISTS reorder_point NUMERIC(14, 4) CHECK (reorder_point >= 0);
