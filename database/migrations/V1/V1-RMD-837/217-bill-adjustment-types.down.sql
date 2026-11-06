ALTER TABLE billing.bill_adjustments DROP CONSTRAINT IF EXISTS ck_bill_adjustments_adjustment_type;
ALTER TABLE billing.bill_adjustments
    ADD CONSTRAINT bill_adjustments_adjustment_type_check
    CHECK (adjustment_type IN ('DiscountPercentage', 'DiscountAmount', 'ServiceFee', 'Kuver', 'Tip', 'CustomFee'));
