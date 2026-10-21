-- Reverse of 196: removes the seeded row only while it still has its seeded values.
DELETE FROM identity.authorization_policies
WHERE permission_code = 'bills.discount' AND role_code = 'waiter' AND mode = 'auto_within'
  AND limit_amount = 100.00 AND max_count = 10 AND window_seconds = 28800 AND updated_by IS NULL AND row_version = 1;
