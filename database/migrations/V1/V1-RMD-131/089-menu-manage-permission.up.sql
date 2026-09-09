-- V1-RMD-131: manager-only permission for the newly wired Menu management
-- API (persistent named menus + the daily-specials lifecycle). Same
-- manager-exclusive pattern as integrations.manage (V12-QRT-003).
INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'menu.manage', 'Manage menus and daily specials')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'menu.manage'
WHERE r.code = 'manager'
ON CONFLICT (role_id, permission_id) DO NOTHING;
