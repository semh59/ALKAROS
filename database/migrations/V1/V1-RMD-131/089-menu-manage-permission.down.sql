DELETE FROM identity.role_permissions
WHERE permission_id = (SELECT permission_id FROM identity.permissions WHERE code = 'menu.manage');

DELETE FROM identity.permissions WHERE code = 'menu.manage';
