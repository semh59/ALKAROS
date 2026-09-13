ALTER TABLE billing.bill_allocations
    DROP CONSTRAINT IF EXISTS fk_bill_allocations_created_by;

ALTER TABLE billing.bill_adjustments
    DROP CONSTRAINT IF EXISTS fk_bill_adjustments_created_by,
    DROP CONSTRAINT IF EXISTS fk_bill_adjustments_authorized_by;
