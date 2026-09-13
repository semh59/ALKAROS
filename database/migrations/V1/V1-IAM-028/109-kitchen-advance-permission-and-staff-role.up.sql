-- V1-IAM-028: split the mutfak (kitchen) forward-transition action from
-- orders.send so a line-cook-only "kitchen-staff" (Mutfak Personeli) role
-- can advance a ticket/item (Queued->Preparing->Ready->Served) without
-- also being able to cancel it, report a problem, suspend a sold-out
-- product or manage printer routing -- those stay behind orders.send /
-- kitchen.reprint / kitchen.routing.manage (Mutfak Sefi, V1-IAM-029).

INSERT INTO identity.permissions (permission_id, code, name) VALUES
    (gen_random_uuid(), 'kitchen.advance', 'Advance a kitchen ticket or item forward one stage')
ON CONFLICT (code) DO NOTHING;

INSERT INTO identity.roles (role_id, code, name) VALUES
    (gen_random_uuid(), 'kitchen-staff', 'Kitchen Staff')
ON CONFLICT (code) DO NOTHING;

-- Every existing FOH role already fires orders to the kitchen (orders.send,
-- which KitchenOperationsEndpoints also accepts for a Cancelled transition)
-- and now also gets kitchen.advance, so nothing they could already do
-- regresses -- only the new kitchen-staff role is limited to this one grant.
INSERT INTO identity.role_permissions (role_permission_id, role_id, permission_id)
SELECT gen_random_uuid(), r.role_id, p.permission_id
FROM identity.roles r
JOIN identity.permissions p ON p.code = 'kitchen.advance'
WHERE r.code IN ('waiter', 'cashier', 'supervisor', 'manager', 'kitchen-staff')
ON CONFLICT (role_id, permission_id) DO NOTHING;
