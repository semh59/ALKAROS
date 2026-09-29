DELETE FROM identity.role_permissions
WHERE permission_id = (SELECT permission_id FROM identity.permissions WHERE code = 'payments.take');

DELETE FROM identity.permissions WHERE code = 'payments.take';
