DELETE FROM identity.role_permissions
WHERE role_id IN (SELECT role_id FROM identity.roles WHERE code = 'manager')
  AND permission_id IN (
      SELECT permission_id FROM identity.permissions
      WHERE code IN (
          'identity.users.manage',
          'identity.roles.manage',
          'identity.permissions.manage',
          'identity.device_sessions.manage')
  );
