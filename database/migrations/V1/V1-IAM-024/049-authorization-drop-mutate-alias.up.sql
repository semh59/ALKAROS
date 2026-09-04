-- V1-IAM-024: every Experience endpoint now checks a granular permission code
-- from docs/domain/authorization-model.md §2-3 (commit A). The transitional
-- pos.cashier.mutate alias -- seeded by migration 042 and re-granted by 043 to
-- cashier/supervisor/manager -- has no remaining reader, so it is dropped here.
--
-- identity.role_permissions rows for the alias disappear via the ON DELETE
-- CASCADE foreign key declared in migration 008. Migration 042 keeps owning
-- catalog.manage and the cashier/supervisor/manager role rows; the `waiter`
-- role (043) never held the alias and is untouched.
DELETE FROM identity.permissions WHERE code = 'pos.cashier.mutate';
