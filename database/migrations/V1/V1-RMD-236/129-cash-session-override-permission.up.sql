-- V1-RMD-236: supervisor-tier permission for closing a cash session with a
-- variance-tolerance override. A cashier holding only cash.drawer must
-- escalate (V1-IAM-019) — mirrors bills.void/comp/discount's own grant.
INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'cash.session.override', 'Close a cash session with a supervisor variance override')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'cash.session.override'
WHERE r.code IN ('supervisor', 'manager')
ON CONFLICT (role_id, permission_id) DO NOTHING;
