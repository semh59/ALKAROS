#!/bin/sh
# ALKAROS PostgreSQL physical base backup for point-in-time recovery (V1-RMD-095).
#
# pg_dump (backup.sh) is a logical snapshot and CANNOT be a PITR base. PITR needs
# a physical base backup plus the archived WAL. This script writes a timestamped
# gzip-compressed tar base backup plus a SHA-256 sidecar into $ALKAROS_BASEBACKUP_DIR.
# Take one after every schema migration and on a daily schedule; the WAL archive
# then lets you recover to any moment after the most recent base backup.
#
# Environment:
#   ALKAROS_DB_HOST        database host              (default: postgres)
#   ALKAROS_DB_PORT        database port              (default: 5432)
#   ALKAROS_DB_USER        database role              (default: alkaros)
#   ALKAROS_BASEBACKUP_DIR output directory           (default: /backups/base)
#   Password: /run/secrets/db_password if present, else $ALKAROS_DB_PASSWORD.
#
# Exit codes: 0 ok | 2 no password | non-zero from pg_basebackup on failure.
set -eu

PGHOST="${ALKAROS_DB_HOST:-postgres}"
PGPORT="${ALKAROS_DB_PORT:-5432}"
PGUSER="${ALKAROS_DB_USER:-alkaros}"
OUT_DIR="${ALKAROS_BASEBACKUP_DIR:-/backups/base}"

if [ -f /run/secrets/db_password ]; then
  PGPASSWORD="$(tr -d '\r\n' < /run/secrets/db_password)"
elif [ -n "${ALKAROS_DB_PASSWORD:-}" ]; then
  PGPASSWORD="$ALKAROS_DB_PASSWORD"
else
  echo "basebackup: no database password (mount /run/secrets/db_password or set ALKAROS_DB_PASSWORD)" >&2
  exit 2
fi
export PGPASSWORD

mkdir -p "$OUT_DIR"
STAMP="$(date -u +%Y%m%dT%H%M%SZ)"
DEST="${OUT_DIR}/alkaros_base_${STAMP}"
mkdir -p "$DEST"

echo "basebackup: pg_basebackup ${PGUSER}@${PGHOST}:${PGPORT} -> ${DEST}"
START="$(date +%s)"
pg_basebackup \
  --host="$PGHOST" --port="$PGPORT" --username="$PGUSER" \
  --pgdata="$DEST" --format=tar --gzip --compress=6 \
  --wal-method=none --checkpoint=fast --progress --no-password
END="$(date +%s)"

# SHA-256 sidecar over the tar members (base.tar.gz [+ others]).
( cd "$DEST" && \
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum ./*.tar.gz > SHA256SUMS
  else
    for f in ./*.tar.gz; do openssl dgst -sha256 -r "$f" | awk '{print $1"  "$2}'; done > SHA256SUMS
  fi )

# pg_basebackup writes the tar members mode 600. Relax to read-only-for-all so a
# later restore-pitr.sh (which runs as the postgres user) can verify and expand
# the archive. The base backup still holds the whole database; keep the parent
# directory tree on trusted storage.
chmod -R a+rX "$DEST"

SIZE="$(du -sb "$DEST" | cut -f1)"
echo "basebackup: ok dir=$(basename "$DEST") bytes=${SIZE} seconds=$((END - START))"
echo "basebackup: WAL archive must be retained from this point for PITR coverage"
