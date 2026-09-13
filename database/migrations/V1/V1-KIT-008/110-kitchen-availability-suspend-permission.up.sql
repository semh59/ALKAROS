-- V1-KIT-008: the Kitchen HTTP surface gets its own
-- "kitchen.availability.suspend" permission code so a chef-level session can
-- 86 a product from the Kitchen screen itself, without a second manager-
-- cookie session (see the task's Goal for why the two session models don't
-- just share one endpoint). This code stays out of the central
-- ApplicationPermissions.cs catalog on purpose -- it is defined locally in
-- KitchenOperationsEndpoints.cs, the same way TicketMutationPermission
-- aliases an existing code rather than adding a new shared constant.
--
-- Granted to 'manager' only for now, so the endpoint is genuinely testable
-- end to end before the role that is actually meant to hold it exists:
-- V1-IAM-029 (Mutfak Sefi) grants this same code to its new role once that
-- role is created -- this migration does not anticipate or block that.

INSERT INTO identity.permissions (permission_id, code, name) VALUES
    (gen_random_uuid(), 'kitchen.availability.suspend',
     'Mark a product unavailable (86) from the Kitchen screen')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'kitchen.availability.suspend'
WHERE r.code = 'manager'
ON CONFLICT (role_id, permission_id) DO NOTHING;
