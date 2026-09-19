DELETE FROM identity.role_permissions
WHERE permission_id = (SELECT permission_id FROM identity.permissions WHERE code = 'cash.session.override');

DELETE FROM identity.permissions WHERE code = 'cash.session.override';
