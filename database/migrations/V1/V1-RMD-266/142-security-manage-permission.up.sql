-- V1-RMD-266: manager-only permission for the newly wired security administration
-- API (account recovery: revoke-all-sessions / force-unlock). Same manager-exclusive
-- pattern as reports.close-day (V1-RMD-249) / integrations.manage (V12-QRT-003).
INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'security.manage', 'Administer accounts and security operations')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'security.manage'
WHERE r.code = 'manager'
ON CONFLICT (role_id, permission_id) DO NOTHING;
