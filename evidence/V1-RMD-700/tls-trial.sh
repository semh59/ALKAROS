#!/bin/sh
# Real-TLS and real-role trial of V1-RMD-700 in ONE disposable container (never the live stack):
# the same entrypoint wrapper, postgresql.tuned.conf and pg_hba.conf compose.yaml gives the `postgres` service,
# a freshly generated CA/certificate, and deploy/docker/roles.sql. Prints one PASS/FAIL line per check.
# Usage: sh evidence/V1-RMD-700/tls-trial.sh   (needs docker, openssl and a psql client on PATH)
set -u
export MSYS_NO_PATHCONV=1

REPO="$(cd "$(dirname "$0")/../.." && pwd)"
NAME="alkaros-rmd700-trial-$$"
PORT="${ALKAROS_TRIAL_PORT:-55700}"
IMAGE="postgres:18@sha256:4ef4dbc939d61acea57712655ddb4b4ab27419c913f94cca0cd57cb3ea3c2280"
WORK="$(mktemp -d "$REPO/.trial.XXXXXX")"
OTHER="$(mktemp -d "$REPO/.trial.XXXXXX")"
FAILS=0

win() { if command -v cygpath >/dev/null 2>&1; then cygpath -m "$1"; else echo "$1"; fi; }
check() { # check <name> <exit-status-wanted-0>
  if [ "$2" -eq 0 ]; then echo "PASS $1"; else echo "FAIL $1"; FAILS=$((FAILS + 1)); fi
}
cleanup() {
  docker rm -f "$NAME" >/dev/null 2>&1
  rm -f "$WORK"/* "$OTHER"/* 2>/dev/null
  rmdir "$WORK" "$OTHER" 2>/dev/null
}
trap cleanup EXIT

sh "$REPO/deploy/docker/generate-db-tls.sh" "$WORK" >/dev/null
sh "$REPO/deploy/docker/generate-db-tls.sh" "$OTHER" >/dev/null
OWNER_PW="$(openssl rand -hex 16)"
APP_PW="$(openssl rand -hex 16)"
RESTORE_PW="$(openssl rand -hex 16)"

# The entrypoint wrapper is read from compose.yaml itself so this cannot drift from what is deployed
# (`docker compose config` shows a literal $ as $$).
WRAPPER="$(cd "$REPO" && docker compose config --format json | python -c "import json,sys; print(json.load(sys.stdin)['services']['postgres']['entrypoint'][2])" | sed 's/\$\$/$/g')"

docker run -d --name "$NAME" --shm-size 512m -p "127.0.0.1:$PORT:5432" \
  -e POSTGRES_DB=alkaros -e POSTGRES_USER=alkaros -e POSTGRES_PASSWORD="$OWNER_PW" \
  -v "$(win "$REPO/deploy/docker/postgresql.tuned.conf"):/etc/postgresql/postgresql.conf:ro" \
  -v "$(win "$REPO/deploy/docker/pg_hba.conf"):/etc/postgresql/pg_hba.conf:ro" \
  -v "$(win "$WORK/db_tls.key"):/run/secrets/db_tls_key:ro" \
  -v "$(win "$WORK/db_tls.crt"):/run/secrets/db_tls_cert:ro" \
  --entrypoint bash "$IMAGE" -c "$WRAPPER" -- postgres -c config_file=/etc/postgresql/postgresql.conf >/dev/null

READY=1
for _ in $(seq 1 60); do
  if docker exec "$NAME" pg_isready -U alkaros -d alkaros >/dev/null 2>&1; then READY=0; break; fi
  sleep 1
done
check "server started with the production entrypoint, config and pg_hba" "$READY"
[ "$READY" -eq 0 ] || { docker logs "$NAME" 2>&1 | tail -15; exit 1; }

CA="$(win "$WORK/db_tls_ca.crt")"
WRONG_CA="$(win "$OTHER/db_tls_ca.crt")"
conn() { # conn <user> <password> <sslmode> <rootcert> <sql>
  PGPASSWORD="$2" psql -X -q -t -A -w "host=localhost port=$PORT dbname=alkaros user=$1 sslmode=$3 sslrootcert=$4" -c "$5" 2>&1
}

KEY_MODE="$(docker exec "$NAME" stat -c '%a %U' /run/alkaros-tls/server.key)"
[ "$KEY_MODE" = "600 postgres" ]; check "server key is 0600 and owned by postgres ($KEY_MODE)" $?

OUT="$(conn alkaros "$OWNER_PW" verify-full "$CA" "SELECT ssl FROM pg_stat_ssl WHERE pid = pg_backend_pid();")"
[ "$OUT" = "t" ]; check "verify-full connection against the generated CA is encrypted" $?

OUT="$(conn alkaros "$OWNER_PW" disable "$CA" "SELECT 1;")"
echo "$OUT" | grep -q "rejects connection"; check "plain-text (sslmode=disable) connection is rejected" $?

OUT="$(conn alkaros "$OWNER_PW" verify-full "$WRONG_CA" "SELECT 1;")"
echo "$OUT" | grep -qi "certificate verify failed"; check "verify-full against a different CA is refused" $?

# roles.sql as the owner, over TLS, with the passwords in the environment only
roles() {
  APP_DB_PASSWORD="$1" RESTORE_DB_PASSWORD="$RESTORE_PW" PGPASSWORD="$OWNER_PW" \
    psql -X -q -w -v ON_ERROR_STOP=1 "host=localhost port=$PORT dbname=alkaros user=alkaros sslmode=verify-full sslrootcert=$CA" \
    -f "$(win "$REPO/deploy/docker/roles.sql")" 2>&1
}
OUT="$(roles "$APP_PW")"; check "roles.sql applies" $?
OUT="$(roles "$APP_PW")"; check "roles.sql applies a second time unchanged" $?

OUT="$(conn alkaros_app "$APP_PW" verify-full "$CA" "SELECT rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication FROM pg_roles WHERE rolname = current_user;")"
[ "$OUT" = "f" ]; check "alkaros_app logs in with its password over TLS and holds no administrative attribute" $?
OUT="$(conn alkaros_app "wrong-$APP_PW" verify-full "$CA" "SELECT 1;")"
echo "$OUT" | grep -q "password authentication failed"; check "alkaros_app with a wrong password is refused" $?
for statement in "CREATE DATABASE trial_nope;" "CREATE ROLE trial_nope;" "CREATE TABLE public.trial_nope (id int);" "CREATE SCHEMA trial_nope;"; do
  OUT="$(conn alkaros_app "$APP_PW" verify-full "$CA" "$statement")"
  echo "$OUT" | grep -q "permission denied"; check "alkaros_app cannot: $statement" $?
done

OUT="$(conn alkaros_restore "$RESTORE_PW" verify-full "$CA" "CREATE DATABASE trial_scratch;")"
[ -z "$OUT" ] || echo "$OUT" | grep -q "CREATE DATABASE"; check "alkaros_restore creates a scratch database" $?
OUT="$(conn alkaros_restore "$RESTORE_PW" verify-full "$CA" "DROP DATABASE trial_scratch;")"
[ -z "$OUT" ] || echo "$OUT" | grep -q "DROP DATABASE"; check "alkaros_restore drops its scratch database" $?

NEW_PW="$(openssl rand -hex 16)"
OUT="$(roles "$NEW_PW")"; check "roles.sql rotates the application password" $?
OUT="$(conn alkaros_app "$APP_PW" verify-full "$CA" "SELECT 1;")"
echo "$OUT" | grep -q "password authentication failed"; check "the old application password stops working" $?
OUT="$(conn alkaros_app "$NEW_PW" verify-full "$CA" "SELECT 1;")"
[ "$OUT" = "1" ]; check "the new application password works" $?

echo "failures=$FAILS"
[ "$FAILS" -eq 0 ]
