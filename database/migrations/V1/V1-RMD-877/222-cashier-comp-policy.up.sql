-- V1-RMD-877 (Product Owner, F0223): a cashier may comp up to 50 TRY at a time, at most 5 of them in an 8 hour window;
-- anything above goes to a manager. A policy row that already exists (a manager set it) is left alone.
INSERT INTO identity.authorization_policies (permission_code, role_code, mode, limit_amount, max_count, window_seconds)
VALUES ('bills.comp', 'cashier', 'auto_within', 50.00, 5, 28800)
ON CONFLICT (permission_code, role_code) DO NOTHING;
