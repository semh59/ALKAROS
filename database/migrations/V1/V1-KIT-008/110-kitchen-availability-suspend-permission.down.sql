-- Reverse of 110. identity.role_permissions rows for this code disappear via
-- ON DELETE CASCADE on identity.role_permissions.permission_id.

DELETE FROM identity.permissions WHERE code = 'kitchen.availability.suspend';
