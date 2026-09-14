-- V1-IAM-030: Semih's decision (2026-09-14) resolves the open question
-- V1-IAM-029 deliberately left unanswered -- the kitchen-chef ("Mutfak
-- Sefi") role now also holds kitchen.routing.manage, sharing the printer/
-- route management screen with supervisor/manager. kitchen.routing.manage
-- already exists in identity.permissions (migration 042); this only adds
-- the one role_permissions row.

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'kitchen.routing.manage'
WHERE r.code = 'kitchen-chef'
ON CONFLICT (role_id, permission_id) DO NOTHING;
