-- The rows removed here are derived data; a rebuild of the counters records them again.
DELETE FROM menu.daily_menu_counter_applied_events WHERE event_type IN ('Returned', 'Wasted');

ALTER TABLE menu.daily_menu_counter_applied_events
    DROP CONSTRAINT IF EXISTS daily_menu_counter_applied_events_event_type_check;

ALTER TABLE menu.daily_menu_counter_applied_events
    ADD CONSTRAINT daily_menu_counter_applied_events_event_type_check
    CHECK (event_type IN ('ProductionOutput', 'Reserved', 'Terminal'));
