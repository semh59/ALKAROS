-- Migration 059: Stock Movement Reversal Unique Constraint (V11-INV-003)
CREATE UNIQUE INDEX IF NOT EXISTS uq_stock_movements_single_reversal
    ON inventory.stock_movements (source_reference_id)
    WHERE movement_type = 'Reversal' AND source_type = 'StockMovement';
