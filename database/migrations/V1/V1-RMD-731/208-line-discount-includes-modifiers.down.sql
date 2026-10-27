-- NOT VALID: lines comped with a priced modifier since the up migration would otherwise block the rollback.
ALTER TABLE billing.bill_items DROP CONSTRAINT IF EXISTS ck_bill_items_nonnegative_amounts;
ALTER TABLE billing.bill_items
    ADD CONSTRAINT ck_bill_items_nonnegative_amounts
    CHECK (
        discount_amount >= 0
        AND discount_amount <= round(quantity * unit_price, 2)
        AND tax_amount >= 0
        AND net_amount >= 0
        AND gross_amount >= 0
        AND gross_amount = net_amount + tax_amount
    ) NOT VALID;

ALTER TABLE orders.order_items DROP CONSTRAINT IF EXISTS ck_order_items_nonnegative_amounts;
ALTER TABLE orders.order_items
    ADD CONSTRAINT ck_order_items_nonnegative_amounts
    CHECK (
        discount_amount >= 0
        AND discount_amount <= round(quantity * unit_price, 2)
        AND tax_amount >= 0
        AND net_amount >= 0
        AND gross_amount >= 0
        AND gross_amount = net_amount + tax_amount
    ) NOT VALID;
