INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'orders.add-without-kitchen', 'Add an item to an open check without a kitchen ticket')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'orders.add-without-kitchen'
WHERE r.code IN ('cashier', 'supervisor', 'manager')
ON CONFLICT (role_id, permission_id) DO NOTHING;
