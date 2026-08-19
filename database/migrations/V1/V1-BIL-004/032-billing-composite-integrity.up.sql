-- 032-billing-composite-integrity.up.sql
-- Additive migration for financial integrity constraints and composite foreign keys (DB-01 / H-12)

-- 1. Ensure composite unique constraint on bill_items (bill_id, bill_item_id)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'uq_bill_items_bill_item'
    ) THEN
        ALTER TABLE billing.bill_items
            ADD CONSTRAINT uq_bill_items_bill_item UNIQUE (bill_id, bill_item_id);
    END IF;
END $$;

-- 2. Upgrade bill_allocations item foreign key to composite (bill_id, bill_item_id)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_bill_allocations_item'
    ) THEN
        ALTER TABLE billing.bill_allocations DROP CONSTRAINT fk_bill_allocations_item;
    END IF;

    ALTER TABLE billing.bill_allocations
        ADD CONSTRAINT fk_bill_allocations_item
        FOREIGN KEY (bill_id, bill_item_id)
        REFERENCES billing.bill_items (bill_id, bill_item_id)
        ON DELETE CASCADE;
END $$;

-- 3. Upgrade bill_adjustments item foreign key to composite (bill_id, bill_item_id)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_bill_adjustments_item'
    ) THEN
        ALTER TABLE billing.bill_adjustments DROP CONSTRAINT fk_bill_adjustments_item;
    END IF;

    ALTER TABLE billing.bill_adjustments
        ADD CONSTRAINT fk_bill_adjustments_item
        FOREIGN KEY (bill_id, bill_item_id)
        REFERENCES billing.bill_items (bill_id, bill_item_id)
        ON DELETE CASCADE;
END $$;
