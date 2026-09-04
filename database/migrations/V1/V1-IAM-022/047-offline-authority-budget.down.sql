-- Reverse of 047. Drops the offline-authority tables; identity.authorization_grants
-- is left untouched (this migration never altered it).
DROP TABLE IF EXISTS identity.offline_authority_replays;
DROP TABLE IF EXISTS identity.offline_authority_budget_lines;
DROP TABLE IF EXISTS identity.offline_authority_budgets;
