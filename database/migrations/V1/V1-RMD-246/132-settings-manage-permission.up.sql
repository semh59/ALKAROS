-- V1-RMD-246: manager-only permission for the newly wired Settings
-- management API (ISettingsService.SetValueAsync/DeactivateAsync existed
-- since V1-SET-001 with zero HTTP surface). Same manager-exclusive pattern
-- as menu.manage (V1-RMD-131) / purchasing.manage (V1-RMD-132) /
-- production.manage (V1-RMD-133).
INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'settings.manage', 'Manage typed system/module settings')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'settings.manage'
WHERE r.code = 'manager'
ON CONFLICT (role_id, permission_id) DO NOTHING;
