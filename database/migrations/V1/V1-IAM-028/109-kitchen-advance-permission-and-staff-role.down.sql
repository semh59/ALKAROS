-- Reverse of 109. identity.role_permissions rows for these disappear via
-- the ON DELETE CASCADE foreign keys from migration 008.
DELETE FROM identity.permissions WHERE code = 'kitchen.advance';

DELETE FROM identity.roles WHERE code = 'kitchen-staff';
