#!/bin/sh
# ALKAROS PostgreSQL local backup (V1-RMD-086).
#
# Produces a timestamped pg_dump custom-format artifact plus a SHA-256 sidecar
# in $ALKAROS_BACKUP_DIR. Custom format lets pg_restore run selectively and in
# parallel, and is compressed on the wire.
#
# Environment:
#   ALKAROS_DB_HOST      database host          (default: postgres)
#   ALKAROS_DB_PORT      database port          (default: 5432)
#   ALKAROS_DB_USER      database role          (default: alkaros)
#   ALKAROS_DB_NAME      database to dump       (default: alkaros)
#   ALKAROS_BACKUP_DIR   output directory       (default: /backups)
#   Password: /run/secrets/db_password if present, else $ALKAROS_DB_PASSWORD.
#
# Exit codes: 0 ok | 2 no password | non-zero from pg_dump on failure.
set -eu

PGHOST="${ALKAROS_DB_HOST:-postgres}"
PGPORT="${ALKAROS_DB_PORT:-5432}"
PGUSER="${ALKAROS_DB_USER:-alkaros}"
PGDATABASE="${ALKAROS_DB_NAME:-alkaros}"
OUT_DIR="${ALKAROS_BACKUP_DIR:-/backups}"

if [ -f /run/secrets/db_password ]; then
  PGPASSWORD="$(tr -d '\r\n' < /run/secrets/db_password)"
elif [ -n "${ALKAROS_DB_PASSWORD:-}" ]; then
  PGPASSWORD="$ALKAROS_DB_PASSWORD"
else
  echo "backup: no database password (mount /run/secrets/db_password or set ALKAROS_DB_PASSWORD)" >&2
  exit 2
fi
export PGPASSWORD

mkdir -p "$OUT_DIR"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
BASENAME="alkaros_${PGDATABASE}_${STAMP}.dump"
ARTIFACT="${OUT_DIR}/${BASENAME}"

echo "backup: pg_dump ${PGUSER}@${PGHOST}:${PGPORT}/${PGDATABASE} -> ${ARTIFACT}"
START="$(date +%s)"
pg_dump \
  --host="$PGHOST" --port="$PGPORT" --username="$PGUSER" --dbname="$PGDATABASE" \
  --format=custom --no-owner --no-privileges --compress=6 \
  --file="$ARTIFACT"
END="$(date +%s)"

# SHA-256 sidecar (portable across postgres:18 base images).
if command -v sha256sum >/dev/null 2>&1; then
  ( cd "$OUT_DIR" && sha256sum "$BASENAME" > "${BASENAME}.sha256" )
else
  ( cd "$OUT_DIR" && openssl dgst -sha256 -r "$BASENAME" | awk '{print $1"  "$2}' > "${BASENAME}.sha256" )
fi

SIZE="$(wc -c < "$ARTIFACT")"
SUM="$(cut -d' ' -f1 < "${ARTIFACT}.sha256")"
echo "backup: ok artifact=${BASENAME} bytes=${SIZE} seconds=$((END - START)) sha256=${SUM}"
