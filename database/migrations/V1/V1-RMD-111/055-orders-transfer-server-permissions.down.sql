DELETE FROM identity.role_permissions
WHERE permission_id IN (
    SELECT permission_id FROM identity.permissions
    WHERE code IN ('orders.transfer-server', 'orders.transfer-server-any')
);

DELETE FROM identity.permissions
WHERE code IN ('orders.transfer-server', 'orders.transfer-server-any');
