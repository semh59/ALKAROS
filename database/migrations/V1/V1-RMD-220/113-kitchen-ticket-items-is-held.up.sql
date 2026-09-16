-- V1-RMD-220: KitchenTicketItem.IsHeld (V1-WTR-025's course model) has
-- existed in the domain model since courses shipped, but was never
-- persisted - it lived only on the in-memory object at ticket-creation
-- time and was lost the moment the ticket was reloaded from the database,
-- so the KDS screen could never actually tell a deliberately-held course
-- item apart from a normal Queued one. Same point-in-time-snapshot
-- rationale as is_age_restricted on this same table (V1-RMD-137):
-- immutable once printed, never updated after insert.
ALTER TABLE kitchen.kitchen_ticket_items
    ADD COLUMN IF NOT EXISTS is_held BOOLEAN NOT NULL DEFAULT FALSE;
