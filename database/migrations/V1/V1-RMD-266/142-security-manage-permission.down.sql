DELETE FROM identity.role_permissions
WHERE permission_id = (SELECT permission_id FROM identity.permissions WHERE code = 'security.manage');

DELETE FROM identity.permissions WHERE code = 'security.manage';
