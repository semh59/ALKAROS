-- V1-RMD-250: supervisor-tier permission for the newly wired reconciliation
-- case management API (IReconciliationService existed since V1-REC-001 with
-- zero HTTP surface). Same family as bills.void/bills.comp
-- (cash.session.override, V1-RMD-236) — a per-case exception a supervisor
-- already resolves as part of the daily job.
INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'reconciliation.manage', 'Investigate and resolve reconciliation discrepancy cases')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'reconciliation.manage'
WHERE r.code IN ('supervisor', 'manager')
ON CONFLICT (role_id, permission_id) DO NOTHING;
