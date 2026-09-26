-- V12-ONL-006: back to one online order per external number regardless of platform. That is impossible once two
-- platforms share a number, so the rollback stops with an explicit error instead of dropping data.
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM orders.orders
               WHERE source = 'Online' AND source_external_id IS NOT NULL
               GROUP BY source_external_id HAVING count(*) > 1) THEN
        RAISE EXCEPTION 'V12-ONL-006 rollback refused: two online platforms share an external order number';
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS uq_orders_online_source_external_id
    ON orders.orders (source_external_id)
    WHERE source = 'Online';

DROP TABLE IF EXISTS online_ordering.online_orders;
