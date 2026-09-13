-- V1-RMD-189: found by the 2026-09-12 five-agent independent Garson audit
-- (database schema dimension) — four identity.* tables held a UUID column
-- that is, in practice, always a real identity.users(user_id) but carried
-- no foreign key enforcing it: a typo'd id, a stale seed script, or a
-- deleted-and-recreated user with a new id would silently leave an
-- unenforced dangling reference, discoverable only by a join that happens
-- to come up empty. Every other module in this codebase already FKs its
-- own "who did this" column to identity.users (table_transfers,
-- table_merges, table_reservations, push_subscriptions,
-- serving_handoff_notes, help_requests, alerts...); these four were the
-- gap, not a deliberate exception.
--
-- authorization_grants and behavioural_tightenings both carry an
-- append-once/append-once-clear trigger that unconditionally rejects any
-- UPDATE on an already-resolved/already-cleared row — including one an
-- ON DELETE SET NULL cascade would itself issue. RESTRICT (the default,
-- no ON DELETE clause) avoids ever triggering that UPDATE at all: deleting
-- a user with grant/tightening history is refused outright, which is the
-- correct behavior for permanent audit data anyway. authorization_policies
-- carries no such trigger (a policy row is ordinarily edited) and its
-- updated_by is exactly the same shape as 026-typed-settings.up.sql's own
-- changed_by column, so it gets that same ON DELETE SET NULL.

ALTER TABLE identity.authorization_grants
    ADD CONSTRAINT fk_authorization_grants_requester
        FOREIGN KEY (requester_user_id) REFERENCES identity.users (user_id),
    ADD CONSTRAINT fk_authorization_grants_approver
        FOREIGN KEY (approver_user_id) REFERENCES identity.users (user_id),
    ADD CONSTRAINT fk_authorization_grants_subject_serving_user
        FOREIGN KEY (subject_serving_user_id) REFERENCES identity.users (user_id);

ALTER TABLE identity.authorization_delegations
    ADD CONSTRAINT fk_authorization_delegations_grantee
        FOREIGN KEY (grantee_user_id) REFERENCES identity.users (user_id),
    ADD CONSTRAINT fk_authorization_delegations_delegator
        FOREIGN KEY (delegator_user_id) REFERENCES identity.users (user_id);

ALTER TABLE identity.behavioural_tightenings
    ADD CONSTRAINT fk_behavioural_tightenings_user
        FOREIGN KEY (user_id) REFERENCES identity.users (user_id),
    ADD CONSTRAINT fk_behavioural_tightenings_cleared_by
        FOREIGN KEY (cleared_by_user_id) REFERENCES identity.users (user_id);

ALTER TABLE identity.authorization_policies
    ADD CONSTRAINT fk_authorization_policies_updated_by
        FOREIGN KEY (updated_by) REFERENCES identity.users (user_id) ON DELETE SET NULL;
