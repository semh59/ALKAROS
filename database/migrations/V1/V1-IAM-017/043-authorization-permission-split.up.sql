-- V1-IAM-017: split the coarse pos.cashier.mutate grant into one code per
-- protected command family (docs/domain/authorization-model.md §2-3) and add the
-- `waiter` role. Migration 042 (V1-RMD-097) stays the owner of pos.cashier.mutate,
-- catalog.manage and the cashier/supervisor/manager rows; this migration only
-- adds and grants the new vocabulary. Endpoints still check pos.cashier.mutate
-- until V1-IAM-024, so cashier/supervisor/manager keep that grant here; `waiter`
-- deliberately never receives it.

-- 1. New application permission codes.
INSERT INTO identity.permissions (permission_id, code, name) VALUES
    ('2a000000-0000-0000-0000-000000000006', 'orders.create',     'Create and edit an unsent order'),
    ('2a000000-0000-0000-0000-000000000007', 'orders.send',        'Fire an order or course to the kitchen'),
    ('2a000000-0000-0000-0000-000000000008', 'tables.status',      'Change table status on own-served tables'),
    ('2a000000-0000-0000-0000-000000000009', 'tables.reserve',     'Create, cancel or claim a manual reservation'),
    ('2a000000-0000-0000-0000-00000000000a', 'tables.transfer',    'Transfer an open order or bill between tables'),
    ('2a000000-0000-0000-0000-00000000000b', 'tables.merge',       'Merge or unmerge tables'),
    ('2a000000-0000-0000-0000-00000000000c', 'floorplan.manage',   'Edit zones, table layout and capacities'),
    ('2a000000-0000-0000-0000-00000000000d', 'bills.split',        'Operational bill splitting'),
    ('2a000000-0000-0000-0000-00000000000e', 'bills.void',         'Void a sent item'),
    ('2a000000-0000-0000-0000-00000000000f', 'bills.comp',         'Zero-price a delivered item'),
    ('2a000000-0000-0000-0000-000000000010', 'bills.discount',     'Apply a line or bill discount'),
    ('2a000000-0000-0000-0000-000000000011', 'cash.drawer',        'No-sale, drawer open or count'),
    ('2a000000-0000-0000-0000-000000000012', 'reports.view',       'View operational reports')
ON CONFLICT (code) DO NOTHING;

-- 2. New role. cashier/supervisor/manager already exist from migration 042.
INSERT INTO identity.roles (role_id, code, name) VALUES
    ('2b000000-0000-0000-0000-000000000004', 'waiter', 'Waiter')
ON CONFLICT (code) DO NOTHING;

-- 3. Granular grants held outright per docs/domain/authorization-model.md §3.
--    Joined on code so pre-existing rows (any id) are matched.
--    waiter        : orders.create, orders.send, tables.status
--    + cashier     : tables.reserve/transfer/merge, bills.split, cash.drawer
--    + supervisor  : floorplan.manage, reports.view, bills.void/comp/discount
--    + manager     : same as supervisor (catalog.manage already granted by 042)
INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON (
        (r.code IN ('waiter', 'cashier', 'supervisor', 'manager')
             AND p.code IN ('orders.create', 'orders.send', 'tables.status'))
     OR (r.code IN ('cashier', 'supervisor', 'manager')
             AND p.code IN ('tables.reserve', 'tables.transfer', 'tables.merge',
                            'bills.split', 'cash.drawer'))
     OR (r.code IN ('supervisor', 'manager')
             AND p.code IN ('floorplan.manage', 'reports.view',
                            'bills.void', 'bills.comp', 'bills.discount'))
    )
ON CONFLICT (role_id, permission_id) DO NOTHING;

-- 4. Transitional alias: every role that already holds pos.cashier.mutate keeps
--    it (endpoints still check it). `waiter` is excluded by construction, which
--    is what blocks a waiter reserving a table at any terminal today.
INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'pos.cashier.mutate'
WHERE r.code IN ('cashier', 'supervisor', 'manager')
ON CONFLICT (role_id, permission_id) DO NOTHING;
