-- V1-RMD-224: found by an independent audit (2026-09-16) -
-- PostgresKitchenTicketRepository.GetCompletedTicketTimingsAsync (the
-- Kitchen performance report's backend, V1-KIT-014) runs
-- WHERE created_at >= @window_start AND created_at < @window_end AND
-- ready_at IS NOT NULL ORDER BY created_at - a range + non-null filter
-- neither of this table's two existing indexes (order_id;
-- station_id, status) supports. Same partial-index shape
-- ix_orders_pending_confirmation (V1-RMD-156) already uses for an
-- analogous filtered range query: index only the rows that can ever
-- match, so the index stays small as the table accumulates months of
-- tickets.
CREATE INDEX IF NOT EXISTS ix_kitchen_tickets_completed_timing
    ON kitchen.kitchen_tickets (created_at)
    WHERE ready_at IS NOT NULL;
