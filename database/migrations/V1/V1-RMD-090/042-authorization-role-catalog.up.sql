-- Deep-analysis finding B-1: the authorization engine (identity.roles /
-- identity.permissions / identity.role_permissions + AuthorizationService) is
-- real and wired into the serve path, but the only role/permission grants that
-- ever reach the database came from `provision-manager`, which seeded just two
-- codes (pos.cashier.mutate, catalog.manage) into a single `manager` role.
--
-- Three further permission codes are referenced by RequirePermissionAsync in the
-- KitchenOperations endpoints - kitchen.routing.manage, kitchen.reprint,
-- operations.backup - but were never inserted into identity.permissions and
-- never granted to any role, so those endpoints returned 403 for every user,
-- including the provisioned manager.
--
-- This migration makes the permission catalog and the three governance roles
-- from V1-GOV-009 the single source of truth. `provision-manager` still creates
-- the `manager` role and binds the bootstrap user; its upsert-by-code re-applies
-- the grants below idempotently.

-- 1. Application permission catalog (distinct from the identity.* admin
--    permissions seeded in migration 008).
INSERT INTO identity.permissions (permission_id, code, name) VALUES
    ('2a000000-0000-0000-0000-000000000001', 'pos.cashier.mutate',
     'Mutate cashier resources'),
    ('2a000000-0000-0000-0000-000000000002', 'catalog.manage',
     'Manage catalog resources'),
    ('2a000000-0000-0000-0000-000000000003', 'kitchen.routing.manage',
     'Manage kitchen printer routing'),
    ('2a000000-0000-0000-0000-000000000004', 'kitchen.reprint',
     'Authorize kitchen ticket reprints'),
    ('2a000000-0000-0000-0000-000000000005', 'operations.backup',
     'Trigger and inspect operational backups')
ON CONFLICT (code) DO NOTHING;

-- 2. Governance roles (V1-GOV-009). Only `manager` is bound to a user, by
--    `provision-manager`; `cashier` and `supervisor` are seeded so operators can
--    be assigned without a schema change. Kitchen-operator and customer-display
--    are intentionally omitted here: the current permission vocabulary is too
--    coarse to express "advance kitchen tickets but not mutate billing" without
--    over-granting pos.cashier.mutate, and that split is deferred to a dedicated
--    authorization wave.
INSERT INTO identity.roles (role_id, code, name) VALUES
    ('2b000000-0000-0000-0000-000000000001', 'cashier', 'Cashier'),
    ('2b000000-0000-0000-0000-000000000002', 'supervisor', 'Supervisor'),
    ('2b000000-0000-0000-0000-000000000003', 'manager', 'Manager')
ON CONFLICT (code) DO NOTHING;

-- 3. Role -> permission grants. Joined on code so the pre-existing permission
--    rows (whatever their id) are picked up too.
INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON (
        (r.code = 'cashier'    AND p.code IN ('pos.cashier.mutate'))
     OR (r.code = 'supervisor' AND p.code IN ('pos.cashier.mutate', 'kitchen.reprint'))
     OR (r.code = 'manager'    AND p.code IN (
            'pos.cashier.mutate', 'catalog.manage',
            'kitchen.routing.manage', 'kitchen.reprint', 'operations.backup'))
    )
ON CONFLICT (role_id, permission_id) DO NOTHING;
