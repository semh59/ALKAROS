-- A transfer between locations is written to the ledger as two Transfer movements (Out at the source, In at the destination).
ALTER TABLE inventory.stock_movements DROP CONSTRAINT IF EXISTS ck_stock_movements_movement_type;
ALTER TABLE inventory.stock_movements
    ADD CONSTRAINT ck_stock_movements_movement_type CHECK (movement_type IN (
        'PurchaseReceipt', 'ProductionOutput', 'Consumption', 'Reservation',
        'Release', 'Waste', 'Adjustment', 'Return', 'Reversal', 'Transfer'
    ));
