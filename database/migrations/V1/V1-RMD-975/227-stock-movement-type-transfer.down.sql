-- Fails if a Transfer movement exists: the ledger is immutable, so such rows cannot be removed and rolling back is only possible before the first transfer.
ALTER TABLE inventory.stock_movements DROP CONSTRAINT IF EXISTS ck_stock_movements_movement_type;
ALTER TABLE inventory.stock_movements
    ADD CONSTRAINT ck_stock_movements_movement_type CHECK (movement_type IN (
        'PurchaseReceipt', 'ProductionOutput', 'Consumption', 'Reservation',
        'Release', 'Waste', 'Adjustment', 'Return', 'Reversal'
    ));
