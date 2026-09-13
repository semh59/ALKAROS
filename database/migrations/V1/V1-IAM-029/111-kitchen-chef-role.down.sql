-- Reverse of 111. identity.role_permissions rows for this role disappear via
-- ON DELETE CASCADE on identity.role_permissions.role_id.

DELETE FROM identity.roles WHERE code = 'kitchen-chef';
