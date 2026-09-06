-- V1-RMD-111: two-tier hand-off permission, following the pattern
-- competitor POS systems (Toast's "Change Server", Lightspeed's "Table
-- Ownership") use — a self-service tier every role already taking orders
-- can exercise without a manager, and a broader tier for handing off
-- someone else's open orders. Distinct from tables.transfer, which moves a
-- table's physical/floor association, not who is accountable for an order.
INSERT INTO identity.permissions (permission_id, code, name) VALUES
    ('2a000000-0000-0000-0000-000000000013', 'orders.transfer-server',
     'Hand off one''s own open orders to another server'),
    ('2a000000-0000-0000-0000-000000000014', 'orders.transfer-server-any',
     'Hand off any server''s open orders to another server')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON (
        (r.code IN ('waiter', 'cashier', 'supervisor', 'manager')
             AND p.code = 'orders.transfer-server')
     OR (r.code IN ('cashier', 'supervisor', 'manager')
             AND p.code = 'orders.transfer-server-any')
    )
ON CONFLICT (role_id, permission_id) DO NOTHING;
