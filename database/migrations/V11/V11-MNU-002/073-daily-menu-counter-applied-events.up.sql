-- Migration 073: DailyMenu Counter Applied Events for Idempotency (V11-MNU-002)
CREATE SCHEMA IF NOT EXISTS menu;

CREATE TABLE IF NOT EXISTS menu.daily_menu_counter_applied_events (
    id UUID PRIMARY KEY,
    daily_menu_item_id UUID NOT NULL REFERENCES menu.daily_menu_items(daily_menu_item_id) ON DELETE CASCADE,
    event_type VARCHAR(32) NOT NULL CHECK (event_type IN ('ProductionOutput', 'Reserved', 'Terminal')),
    source_event_id UUID NOT NULL,
    terminal_status VARCHAR(32) NULL CHECK (terminal_status IS NULL OR terminal_status IN ('Released', 'Consumed', 'Waste')),
    quantity NUMERIC(10, 3) NOT NULL CHECK (quantity > 0),
    applied_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_daily_menu_counter_applied_event UNIQUE (daily_menu_item_id, event_type, source_event_id)
);

CREATE INDEX IF NOT EXISTS idx_daily_menu_counter_events_item
    ON menu.daily_menu_counter_applied_events (daily_menu_item_id);

CREATE INDEX IF NOT EXISTS idx_daily_menu_counter_events_source
    ON menu.daily_menu_counter_applied_events (source_event_id);
