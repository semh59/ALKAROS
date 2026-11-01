-- Returned and wasted portions of an already consumed reservation are applied to the daily menu counters once each.
ALTER TABLE menu.daily_menu_counter_applied_events
    DROP CONSTRAINT IF EXISTS daily_menu_counter_applied_events_event_type_check;

ALTER TABLE menu.daily_menu_counter_applied_events
    ADD CONSTRAINT daily_menu_counter_applied_events_event_type_check
    CHECK (event_type IN ('ProductionOutput', 'Reserved', 'Terminal', 'Returned', 'Wasted'));
