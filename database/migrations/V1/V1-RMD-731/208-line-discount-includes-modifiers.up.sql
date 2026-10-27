-- V1-RMD-731: a complimentary line's discount is its whole line value, modifiers included, but the check could
-- only see quantity x unit price, so a comp on an item with a priced modifier was refused. The modifier prices
-- live in another table and cannot be part of a CHECK; the domain (OrderItem, BillItem) keeps the upper bound.
ALTER TABLE orders.order_items DROP CONSTRAINT IF EXISTS ck_order_items_nonnegative_amounts;
ALTER TABLE orders.order_items
    ADD CONSTRAINT ck_order_items_nonnegative_amounts
    CHECK (
        discount_amount >= 0
        AND tax_amount >= 0
        AND net_amount >= 0
        AND gross_amount >= 0
        AND gross_amount = net_amount + tax_amount
    );

ALTER TABLE billing.bill_items DROP CONSTRAINT IF EXISTS ck_bill_items_nonnegative_amounts;
ALTER TABLE billing.bill_items
    ADD CONSTRAINT ck_bill_items_nonnegative_amounts
    CHECK (
        discount_amount >= 0
        AND tax_amount >= 0
        AND net_amount >= 0
        AND gross_amount >= 0
        AND gross_amount = net_amount + tax_amount
    );
