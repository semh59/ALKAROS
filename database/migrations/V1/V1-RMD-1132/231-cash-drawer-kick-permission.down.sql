DELETE FROM identity.role_permissions
WHERE permission_id IN (SELECT permission_id FROM identity.permissions WHERE code = 'cash.drawer.kick');
DELETE FROM identity.permissions WHERE code = 'cash.drawer.kick';
