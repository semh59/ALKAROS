-- Reverse of 043. Removes only what this migration added: the 13 granular
-- application permission codes and the `waiter` role. identity.role_permissions
-- rows for these disappear via the ON DELETE CASCADE foreign keys from migration
-- 008. pos.cashier.mutate, catalog.manage and the cashier/supervisor/manager
-- rows are left in place — migration 042 and provision-manager own them.
DELETE FROM identity.permissions
WHERE code IN (
    'orders.create', 'orders.send', 'tables.status', 'tables.reserve',
    'tables.transfer', 'tables.merge', 'floorplan.manage', 'bills.split',
    'bills.void', 'bills.comp', 'bills.discount', 'cash.drawer', 'reports.view'
);

DELETE FROM identity.roles
WHERE code = 'waiter';
