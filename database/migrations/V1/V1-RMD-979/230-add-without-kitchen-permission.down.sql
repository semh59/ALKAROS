DELETE FROM identity.role_permissions
WHERE permission_id IN (SELECT permission_id FROM identity.permissions WHERE code = 'orders.add-without-kitchen');
DELETE FROM identity.permissions WHERE code = 'orders.add-without-kitchen';
