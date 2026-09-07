-- V12-QRT-003: manager-only permission for configuring a third-party
-- integration credential (the QR relay provider token). First
-- manager-exclusive grant in the catalog — supervisor does not receive it.
INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'integrations.manage', 'Configure a third-party integration credential')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'integrations.manage'
WHERE r.code = 'manager'
ON CONFLICT (role_id, permission_id) DO NOTHING;
