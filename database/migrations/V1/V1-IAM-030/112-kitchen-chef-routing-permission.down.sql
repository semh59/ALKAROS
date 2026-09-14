-- Reverse of 112. Deletes only the kitchen-chef + kitchen.routing.manage
-- grant this migration added -- the kitchen-chef role and the
-- kitchen.routing.manage permission themselves are owned by earlier
-- migrations (111, 042) and are left untouched.

DELETE FROM identity.role_permissions
WHERE role_id = (SELECT role_id FROM identity.roles WHERE code = 'kitchen-chef')
  AND permission_id = (SELECT permission_id FROM identity.permissions WHERE code = 'kitchen.routing.manage');
