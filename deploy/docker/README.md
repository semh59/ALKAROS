# ALKAROS container release

1. Create `deploy/docker/db_password` with a deployment-only database password.
2. Create `deploy/docker/admin_password` with a unique manager password of 12-256 non-whitespace characters.
3. Optionally export `ALKAROS_BOOTSTRAP_USERNAME`, `ALKAROS_BOOTSTRAP_DISPLAY_NAME` or
   `ALKAROS_KITCHEN_STATION_ID`; defaults are `admin`, `ALKAROS Manager` and `kitchen-main`.
4. Run `docker compose up --detach --build --pull always --force-recreate --remove-orphans --wait`.

The one-shot migration/provisioning services and Host read passwords from mounted Docker secrets; passwords are not
required in the Compose environment. Windows CRLF and Unix LF line endings are normalized without logging the secret.
Migration and idempotent manager provisioning must complete before Host starts. Re-running provisioning never resets an
existing password, and provisioning refuses takeover when another user exists without the configured manager. Both real
secret files are ignored by Git and the Docker build context. PostgreSQL 18 is digest pinned and uses the named
`alkaros-postgres` volume. Caddy terminates TLS on port 8443 and forwards the trusted proxy headers to Host. The
certificate is Caddy's internal `localhost` certificate; replace the proxy certificate policy with an approved public certificate
before external go-live. The stack does not claim fiscal, payment-provider, printer or signed go-live readiness.

The Compose project name is fixed to `alkaros`, so rebuilds replace the same service set instead of creating a second
project group when the checkout directory name changes. `--remove-orphans` removes services left by an older ALKAROS
Compose definition; it intentionally does not delete unrelated standalone containers or their data.
