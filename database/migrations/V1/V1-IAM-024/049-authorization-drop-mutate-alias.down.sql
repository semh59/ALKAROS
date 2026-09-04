-- Reverse of 049: restore the pos.cashier.mutate permission row (same id as
-- migration 042) and re-grant it to cashier, supervisor and manager -- the
-- state migrations 042 and 043 leave it in. `waiter` never held it. Joined on
-- code so the pre-existing role rows are matched whatever their id.
INSERT INTO identity.permissions (permission_id, code, name)
VALUES ('2a000000-0000-0000-0000-000000000001', 'pos.cashier.mutate', 'Mutate cashier resources')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'pos.cashier.mutate'
WHERE r.code IN ('cashier', 'supervisor', 'manager')
ON CONFLICT (role_id, permission_id) DO NOTHING;
