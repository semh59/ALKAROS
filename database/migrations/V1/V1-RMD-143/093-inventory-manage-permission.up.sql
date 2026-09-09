-- V1-RMD-143: manager-only permission for the newly wired StockMaster
-- management API (stock locations/items + product stock mappings). Same
-- manager-exclusive pattern as menu.manage (V1-RMD-131) / purchasing.manage
-- (V1-RMD-132) / production.manage (V1-RMD-133).
INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'inventory.manage', 'Manage stock locations, items and product stock mappings')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'inventory.manage'
WHERE r.code = 'manager'
ON CONFLICT (role_id, permission_id) DO NOTHING;
