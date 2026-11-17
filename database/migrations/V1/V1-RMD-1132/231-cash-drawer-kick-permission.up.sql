INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'cash.drawer.kick', 'Open the cash drawer without a sale')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'cash.drawer.kick'
WHERE r.code IN ('cashier', 'supervisor', 'manager')
ON CONFLICT (role_id, permission_id) DO NOTHING;
