-- Reverse of 222: removes the seeded row only while it still has its seeded values.
DELETE FROM identity.authorization_policies
WHERE permission_code = 'bills.comp' AND role_code = 'cashier' AND mode = 'auto_within'
  AND limit_amount = 50.00 AND max_count = 5 AND window_seconds = 28800 AND updated_by IS NULL AND row_version = 1;
