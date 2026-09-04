-- Reverse of 045.
DROP VIEW IF EXISTS reporting.authorization_grant_daily;
DROP TRIGGER IF EXISTS trg_authorization_grants_transition ON identity.authorization_grants;
DROP FUNCTION IF EXISTS identity.enforce_authorization_grant_transition();
DROP TABLE IF EXISTS identity.authorization_grants;
