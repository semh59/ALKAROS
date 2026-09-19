-- V1-RMD-249: manager-only permission for the newly wired EOD business-day
-- open/close API (IOperationalReportService existed since V1-RPT-001 with
-- zero HTTP surface). Same manager-exclusive pattern as settings.manage
-- (V1-RMD-246) / integrations.manage (V12-QRT-003).
INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'reports.close-day', 'Open or close a business day (EOD)')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'reports.close-day'
WHERE r.code = 'manager'
ON CONFLICT (role_id, permission_id) DO NOTHING;
