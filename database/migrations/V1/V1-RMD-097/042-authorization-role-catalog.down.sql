-- Reverse of 042. The `manager` role plus the pos.cashier.mutate and
-- catalog.manage permissions are left in place because provision-manager owns
-- them and a live rollback must not unbind the bootstrap operator. Everything
-- unique to this migration is removed; identity.role_permissions rows disappear
-- via the ON DELETE CASCADE foreign keys declared in migration 008.
DELETE FROM identity.permissions
WHERE code IN ('kitchen.routing.manage', 'kitchen.reprint', 'operations.backup');

DELETE FROM identity.roles
WHERE code IN ('cashier', 'supervisor');
