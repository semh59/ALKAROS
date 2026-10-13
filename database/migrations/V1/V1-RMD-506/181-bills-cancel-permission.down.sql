DELETE FROM identity.role_permissions
WHERE permission_id IN (SELECT permission_id FROM identity.permissions WHERE code = 'bills.cancel');
DELETE FROM identity.permissions WHERE code = 'bills.cancel';
