DROP INDEX IF EXISTS orders.ix_orders_serving_user;

ALTER TABLE orders.orders
    DROP COLUMN IF EXISTS serving_user_id;
