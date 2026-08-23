ALTER TABLE orders.order_items
    ADD CONSTRAINT ck_order_items_nonnegative_amounts
    CHECK (
        discount_amount >= 0
        AND discount_amount <= round(quantity * unit_price, 2)
        AND tax_amount >= 0
        AND net_amount >= 0
        AND gross_amount >= 0
        AND gross_amount = net_amount + tax_amount
    );

ALTER TABLE billing.bill_items
    ADD CONSTRAINT ck_bill_items_nonnegative_amounts
    CHECK (
        discount_amount >= 0
        AND discount_amount <= round(quantity * unit_price, 2)
        AND tax_amount >= 0
        AND net_amount >= 0
        AND gross_amount >= 0
        AND gross_amount = net_amount + tax_amount
    );
