-- V1-RMD-132: manager-only permission for the newly wired Purchasing
-- management API (suppliers, purchase orders, goods receipt). Same
-- manager-exclusive pattern as menu.manage (V1-RMD-131).
INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'purchasing.manage', 'Manage suppliers, purchase orders and goods receipt')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'purchasing.manage'
WHERE r.code = 'manager'
ON CONFLICT (role_id, permission_id) DO NOTHING;
