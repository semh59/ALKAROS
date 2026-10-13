DELETE FROM identity.role_permissions
WHERE permission_id IN (SELECT permission_id FROM identity.permissions
                        WHERE code IN ('payments.refund.request', 'payments.refund.approve'));
DELETE FROM identity.permissions WHERE code IN ('payments.refund.request', 'payments.refund.approve');
