-- V1-IAM-029: "Mutfak Sefi" (executive chef) -- a fifth kitchen-only role,
-- independent from the FOH "supervisor" (sef garson). Holds everything the
-- narrower "kitchen-staff" (Mutfak Personeli, V1-IAM-028) cannot: cancel /
-- report a problem (orders.send, same permission a Cancelled kitchen
-- transition already requires), approve or reject a print-recovery reprint
-- (kitchen.reprint), and 86 a sold-out product from the Kitchen screen
-- (kitchen.availability.suspend, V1-KIT-008). kitchen.advance is included
-- too so the chef is not left unable to do what a line cook already can.
--
-- kitchen.routing.manage is deliberately NOT granted here -- the task's Out
-- of scope: whether a chef should also manage printer routing is an open
-- question for Semih, not yet decided.

INSERT INTO identity.roles (role_id, code, name) VALUES
    (gen_random_uuid(), 'kitchen-chef', 'Kitchen Chef')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code IN (
    'orders.send', 'kitchen.advance', 'kitchen.reprint', 'kitchen.availability.suspend')
WHERE r.code = 'kitchen-chef'
ON CONFLICT (role_id, permission_id) DO NOTHING;
