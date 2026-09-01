#!/bin/sh
# ALKAROS PostgreSQL restore with mandatory checksum verification (V1-RMD-086).
#
# Usage: restore.sh <artifact.dump> [target-database]
#
# The restore is REFUSED unless a <artifact>.sha256 sidecar exists and matches
# the artifact byte-for-byte. A corrupted or truncated artifact never reaches
# pg_restore. The target database is dropped and recreated so the restore starts
# from a clean schema; by default this is a side database, never the live one.
#
# Environment:
#   ALKAROS_DB_HOST      database host                 (default: postgres)
#   ALKAROS_DB_PORT      database port                 (default: 5432)
#   ALKAROS_DB_USER      database role                 (default: alkaros)
#   ALKAROS_RESTORE_DB   default target if arg 2 unset (default: alkaros_restore)
#   Password: /run/secrets/db_password if present, else $ALKAROS_DB_PASSWORD.
#
# Exit codes:
#   0 ok | 2 usage / artifact missing | 3 missing sidecar |
#   4 checksum mismatch (refused) | non-zero from pg_restore on failure.
set -eu

ARTIFACT="${1:-}"
if [ -z "$ARTIFACT" ]; then
  echo "usage: restore.sh <artifact.dump> [target-database]" >&2
  exit 2
fi
TARGET_DB="${2:-${ALKAROS_RESTORE_DB:-alkaros_restore}}"

PGHOST="${ALKAROS_DB_HOST:-postgres}"
PGPORT="${ALKAROS_DB_PORT:-5432}"
PGUSER="${ALKAROS_DB_USER:-alkaros}"

if [ ! -f "$ARTIFACT" ]; then
  echo "restore: artifact not found: $ARTIFACT" >&2
  exit 2
fi

SIDECAR="${ARTIFACT}.sha256"
if [ ! -f "$SIDECAR" ]; then
  echo "restore: missing checksum sidecar ${SIDECAR}; refusing to restore" >&2
  exit 3
fi

EXPECT="$(cut -d' ' -f1 < "$SIDECAR")"
if command -v sha256sum >/dev/null 2>&1; then
  ACTUAL="$(sha256sum "$ARTIFACT" | cut -d' ' -f1)"
else
  ACTUAL="$(openssl dgst -sha256 -r "$ARTIFACT" | cut -d' ' -f1)"
fi

if [ -z "$EXPECT" ] || [ "$EXPECT" != "$ACTUAL" ]; then
  echo "restore: CHECKSUM MISMATCH expected=${EXPECT:-<empty>} actual=${ACTUAL}; refusing to restore" >&2
  exit 4
fi
echo "restore: checksum verified sha256=${ACTUAL}"

if [ -f /run/secrets/db_password ]; then
  PGPASSWORD="$(tr -d '\r\n' < /run/secrets/db_password)"
elif [ -n "${ALKAROS_DB_PASSWORD:-}" ]; then
  PGPASSWORD="$ALKAROS_DB_PASSWORD"
else
  echo "restore: no database password (mount /run/secrets/db_password or set ALKAROS_DB_PASSWORD)" >&2
  exit 2
fi
export PGPASSWORD

echo "restore: recreating clean target database \"${TARGET_DB}\""
psql --host="$PGHOST" --port="$PGPORT" --username="$PGUSER" --dbname=postgres \
  --set=ON_ERROR_STOP=1 --quiet \
  --command="DROP DATABASE IF EXISTS \"${TARGET_DB}\" WITH (FORCE);" \
  --command="CREATE DATABASE \"${TARGET_DB}\";"

echo "restore: pg_restore -> ${PGUSER}@${PGHOST}:${PGPORT}/${TARGET_DB}"
START="$(date +%s)"
pg_restore \
  --host="$PGHOST" --port="$PGPORT" --username="$PGUSER" --dbname="$TARGET_DB" \
  --no-owner --no-privileges --exit-on-error --jobs=2 \
  "$ARTIFACT"
END="$(date +%s)"

echo "restore: ok target=${TARGET_DB} seconds=$((END - START))"
