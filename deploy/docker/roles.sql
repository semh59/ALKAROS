-- ALKAROS runtime database roles. Applied by the owner role after every migration run (the `migrate` service),
-- so tables and schemas a migration adds are granted at once; running it again changes nothing.
--
--   app_role      what `api` and `connector` log in as: data access only. Not a superuser, cannot create databases,
--                 roles, schemas or tables, owns nothing, cannot touch the migration history.
--   restore_role  what the restore drill logs in as: may create (and so drop) its own scratch databases and nothing
--                 else; it has no access to any application table.
--
-- The owner role (the one that runs migrations, backups and the operations commands) is unchanged.
-- Passwords arrive in the psql environment (APP_DB_PASSWORD, RESTORE_DB_PASSWORD), never on a command line.

\set ON_ERROR_STOP on
\if :{?app_role}
\else
\set app_role alkaros_app
\endif
\if :{?restore_role}
\else
\set restore_role alkaros_restore
\endif
\getenv app_password APP_DB_PASSWORD
\getenv restore_password RESTORE_DB_PASSWORD
\if :{?app_password}
\else
DO $$ BEGIN RAISE EXCEPTION 'APP_DB_PASSWORD is not set'; END $$;
\endif
\if :{?restore_password}
\else
DO $$ BEGIN RAISE EXCEPTION 'RESTORE_DB_PASSWORD is not set'; END $$;
\endif

SELECT format('CREATE ROLE %I LOGIN', :'app_role')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'app_role') \gexec
SELECT format('ALTER ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L',
              :'app_role', :'app_password') \gexec

SELECT format('CREATE ROLE %I LOGIN', :'restore_role')
WHERE NOT EXISTS (SELECT FROM pg_roles WHERE rolname = :'restore_role') \gexec
SELECT format('ALTER ROLE %I LOGIN NOSUPERUSER CREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L',
              :'restore_role', :'restore_password') \gexec

SELECT format('GRANT CONNECT ON DATABASE %I TO %I, %I', current_database(), :'app_role', :'restore_role') \gexec

-- Nobody but the owner creates objects in the public schema (the default since PostgreSQL 15, made explicit).
REVOKE CREATE ON SCHEMA public FROM PUBLIC;

SELECT format('GRANT USAGE ON SCHEMA %I TO %I', nspname, :'app_role')
FROM pg_namespace
WHERE nspname NOT LIKE 'pg\_%' AND nspname <> 'information_schema' \gexec
SELECT format('GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA %I TO %I', nspname, :'app_role')
FROM pg_namespace
WHERE nspname NOT LIKE 'pg\_%' AND nspname <> 'information_schema' \gexec
SELECT format('GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA %I TO %I', nspname, :'app_role')
FROM pg_namespace
WHERE nspname NOT LIKE 'pg\_%' AND nspname <> 'information_schema' \gexec
-- Whatever the owner creates later in a schema (before this script runs again) is granted the same way.
SELECT format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO %I',
              nspname, :'app_role')
FROM pg_namespace
WHERE nspname NOT LIKE 'pg\_%' AND nspname <> 'information_schema' \gexec
SELECT format('ALTER DEFAULT PRIVILEGES IN SCHEMA %I GRANT USAGE, SELECT ON SEQUENCES TO %I', nspname, :'app_role')
FROM pg_namespace
WHERE nspname NOT LIKE 'pg\_%' AND nspname <> 'information_schema' \gexec

-- The migration history is the owner's alone: the runtime role must not be able to rewrite what was applied.
SELECT format('REVOKE ALL ON %s FROM %I', c.oid::regclass, :'app_role')
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relname = 'alkaros_schema_migrations' AND c.relkind = 'r' \gexec
