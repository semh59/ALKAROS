DELETE FROM identity.role_permissions
WHERE permission_id = (SELECT permission_id FROM identity.permissions WHERE code = 'production.manage');

DELETE FROM identity.permissions WHERE code = 'production.manage';
