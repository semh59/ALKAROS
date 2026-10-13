-- Asking for a refund is cashier tier; approving or rejecting one is supervisor tier and up.
INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'payments.refund.request', 'Ask for a paid bill to be refunded')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'payments.refund.approve', 'Approve or reject a refund request')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'payments.refund.request'
WHERE r.code IN ('cashier', 'supervisor', 'manager')
ON CONFLICT (role_id, permission_id) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'payments.refund.approve'
WHERE r.code IN ('supervisor', 'manager')
ON CONFLICT (role_id, permission_id) DO NOTHING;
