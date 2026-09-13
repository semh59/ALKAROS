-- V1-RMD-191: found by the 2026-09-12 five-agent independent Garson audit
-- (database schema dimension) — the same gap V1-RMD-189 closed for the
-- four identity.* authorization tables, one schema over: billing's own
-- "who did this" columns (authorized_by, created_by) carried no foreign
-- key to identity.users, even though every other module's equivalent
-- column already does (table_transfers, table_merges,
-- push_subscriptions, serving_handoff_notes, help_requests, alerts,
-- 026-typed-settings's changed_by, and now V1-RMD-189's own four
-- authorization tables). Cross-module FKs to identity.users are this
-- codebase's consistent norm, not an exception this schema was carved
-- out of.
--
-- Neither table carries an immutability trigger (unlike V1-RMD-189's
-- authorization_grants/behavioural_tightenings), so an ON DELETE SET
-- NULL cascade on the nullable created_by columns is unproblematic.
-- authorized_by is NOT NULL - who actually authorized a discount/comp/
-- fee is exactly the kind of fact that must survive the authorizer's
-- own account being deleted, so it keeps the plain (RESTRICT) default.

ALTER TABLE billing.bill_adjustments
    ADD CONSTRAINT fk_bill_adjustments_authorized_by
        FOREIGN KEY (authorized_by) REFERENCES identity.users (user_id),
    ADD CONSTRAINT fk_bill_adjustments_created_by
        FOREIGN KEY (created_by) REFERENCES identity.users (user_id) ON DELETE SET NULL;

ALTER TABLE billing.bill_allocations
    ADD CONSTRAINT fk_bill_allocations_created_by
        FOREIGN KEY (created_by) REFERENCES identity.users (user_id) ON DELETE SET NULL;
