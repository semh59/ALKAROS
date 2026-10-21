-- V1-RMD-674 (Product Owner, 2026-10-21): a waiter approves one discount of up to 100 TRY without a manager, at most
-- 10 of them in an 8 hour window; anything above goes to a manager. Comp and void keep no standing limit.
-- A policy row that already exists (a manager set it) is left alone.
INSERT INTO identity.authorization_policies (permission_code, role_code, mode, limit_amount, max_count, window_seconds)
VALUES ('bills.discount', 'waiter', 'auto_within', 100.00, 10, 28800)
ON CONFLICT (permission_code, role_code) DO NOTHING;
