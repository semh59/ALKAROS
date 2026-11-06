-- AdjustmentType.CustomFee has no factory, endpoint or screen that produces it, yet the column accepted it.
-- The check now lists exactly the types the domain can create. A stored CustomFee row stops the migration:
-- the value could not be read back (the repository parses the column into the enum) and must not be rewritten silently.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM billing.bill_adjustments WHERE adjustment_type = 'CustomFee') THEN
        RAISE EXCEPTION 'billing.bill_adjustments holds CustomFee rows; resolve them before this migration';
    END IF;
END $$;

ALTER TABLE billing.bill_adjustments DROP CONSTRAINT IF EXISTS bill_adjustments_adjustment_type_check;
ALTER TABLE billing.bill_adjustments DROP CONSTRAINT IF EXISTS ck_bill_adjustments_adjustment_type;
ALTER TABLE billing.bill_adjustments
    ADD CONSTRAINT ck_bill_adjustments_adjustment_type
    CHECK (adjustment_type IN ('DiscountPercentage', 'DiscountAmount', 'ServiceFee', 'Kuver', 'Tip'));
