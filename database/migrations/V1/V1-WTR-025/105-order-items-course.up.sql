-- V1-WTR-025: course management (kurs sirala/beklet/atesle). Semih's
-- decision (2026-09-11, full course model): each order item can carry a
-- course number so a multi-course order is entered and fired as one whole
-- plan. course_number is nullable — most items (a quick-sale, a single
-- round with no course structure) never set it and behave exactly as
-- before.
--
-- kitchen_state gains 'Held': Order.FireRound now activates every Draft
-- item in the fired round together (so the kitchen ticket shows the whole
-- planned order up front for prep), but only the lowest course number in
-- that round becomes Sent — every later course becomes Held instead of
-- Sent, printed on the same ticket but marked not to start yet.
-- Order.FireCourse (a separate, explicit action) promotes one course's
-- Held items to Sent when the waiter actually calls it in.
ALTER TABLE orders.order_items
    ADD COLUMN IF NOT EXISTS course_number SMALLINT NULL
        CHECK (course_number BETWEEN 1 AND 20);

CREATE INDEX IF NOT EXISTS ix_order_items_course ON orders.order_items (order_id, course_number)
    WHERE course_number IS NOT NULL;

ALTER TABLE orders.order_items
    DROP CONSTRAINT order_items_kitchen_state_check;

ALTER TABLE orders.order_items
    ADD CONSTRAINT order_items_kitchen_state_check
        CHECK (kitchen_state IN ('NotSent', 'Held', 'Sent', 'Preparing', 'Ready', 'Served', 'Cancelled'));
