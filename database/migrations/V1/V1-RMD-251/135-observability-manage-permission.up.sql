-- V1-RMD-251: supervisor-tier permission for the newly wired alert/health
-- check management API (IAlertService/IObservabilityService existed since
-- V1-ALT-001/V1-OBS-001 with zero HTTP surface). Same family as
-- reconciliation.manage (V1-RMD-250) — a per-case exception a supervisor
-- already resolves as part of the daily job.
INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'observability.manage', 'Manage operational alerts and record health checks')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'observability.manage'
WHERE r.code IN ('supervisor', 'manager')
ON CONFLICT (role_id, permission_id) DO NOTHING;
