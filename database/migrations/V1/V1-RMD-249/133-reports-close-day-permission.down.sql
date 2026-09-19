DELETE FROM identity.role_permissions
WHERE permission_id = (SELECT permission_id FROM identity.permissions WHERE code = 'reports.close-day');

DELETE FROM identity.permissions WHERE code = 'reports.close-day';
