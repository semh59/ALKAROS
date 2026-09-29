-- V1-RMD-401: taking a payment on a bill (card/EFT tender; cash tender also
-- needs cash.drawer). Cashier tier by default; the waiter role does not hold
-- it until a business grants it through role management (PO 2026-09-28).
INSERT INTO identity.permissions (permission_id, code, name)
VALUES (gen_random_uuid(), 'payments.take', 'Take a payment on a bill')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'payments.take'
WHERE r.code IN ('cashier', 'supervisor', 'manager')
ON CONFLICT (role_id, permission_id) DO NOTHING;
