-- Found while designing waiter-table ownership tracking (2026-09-06): the
-- identity.* admin permissions (identity.users.manage, identity.roles.manage,
-- identity.permissions.manage, identity.device_sessions.manage — seeded into
-- identity.permissions by migration 008) were never granted to any role,
-- anywhere. RoleManagementEndpoints (/api/v1/management/roles/**, V1-RMD-102)
-- authenticates a manager/supervisor session correctly but every command then
-- makes its own AuthorizeAsync decision against one of these codes — which
-- always denied, including for the bootstrap manager provisioned by
-- `provision-manager` (that command only ever grants catalog.manage,
-- kitchen.routing.manage, kitchen.reprint, operations.backup). The whole
-- surface was reachable but unusable by anyone.
--
-- Scoped to `manager` only (not `supervisor`): creating staff accounts and
-- reassigning roles is a higher-privilege operation than the floor/escalation
-- permissions supervisor already holds.
INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code IN (
    'identity.users.manage',
    'identity.roles.manage',
    'identity.permissions.manage',
    'identity.device_sessions.manage')
WHERE r.code = 'manager'
ON CONFLICT (role_id, permission_id) DO NOTHING;
