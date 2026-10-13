-- Cancelling an unpaid bill: cashier tier by default (PO 2026-10-13: cashier and manager, reason mandatory).
INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'bills.cancel', 'Cancel an unpaid bill with a reason')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'bills.cancel'
WHERE r.code IN ('cashier', 'supervisor', 'manager')
ON CONFLICT (role_id, permission_id) DO NOTHING;
