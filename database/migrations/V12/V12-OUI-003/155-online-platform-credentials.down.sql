-- V12-OUI-003 rollback: the stored platform settings are dropped; platforms fall back to environment variables.
DROP TABLE IF EXISTS online_ordering.platform_credentials;
