#!/bin/sh
# ALKAROS backup/restore round-trip self-check (V1-RMD-086).
#
# Proves the deploy/docker/backup.sh + restore.sh path end to end against a
# disposable PostgreSQL 18 server, WITHOUT touching the live `alkaros` database:
#
#   1. create a disposable source database and seed a verification table
#   2. backup.sh  -> custom-format artifact + SHA-256 sidecar
#   3. NEGATIVE: corrupt a copy of the artifact; restore.sh MUST refuse it
#   4. POSITIVE: restore.sh the intact artifact into a fresh target database
#   5. compare row count and a data checksum between source and target
#   6. drop both disposable databases
#
# Environment (same password rules as backup.sh / restore.sh):
#   ALKAROS_DB_HOST (default: postgres)   ALKAROS_DB_PORT (default: 5432)
#   ALKAROS_DB_USER (default: alkaros)
#   ALKAROS_DB_PASSWORD or /run/secrets/db_password
#
# Exit code 0 only when every assertion holds.
set -eu

HERE="$(cd "$(dirname "$0")" && pwd)"
PGHOST="${ALKAROS_DB_HOST:-postgres}"
PGPORT="${ALKAROS_DB_PORT:-5432}"
PGUSER="${ALKAROS_DB_USER:-alkaros}"
SRC_DB="bkp_selfcheck_src"
DST_DB="bkp_selfcheck_dst"
WORK_DIR="${ALKAROS_BACKUP_DIR:-/tmp/alkaros-selfcheck}"

if [ -f /run/secrets/db_password ]; then
  PGPASSWORD="$(tr -d '\r\n' < /run/secrets/db_password)"
elif [ -n "${ALKAROS_DB_PASSWORD:-}" ]; then
  PGPASSWORD="$ALKAROS_DB_PASSWORD"
else
  echo "selfcheck: no database password" >&2
  exit 2
fi
export PGPASSWORD ALKAROS_DB_HOST="$PGHOST" ALKAROS_DB_PORT="$PGPORT" ALKAROS_DB_USER="$PGUSER"
export ALKAROS_BACKUP_DIR="$WORK_DIR"

psql_admin() {
  psql --host="$PGHOST" --port="$PGPORT" --username="$PGUSER" --dbname=postgres \
    --set=ON_ERROR_STOP=1 --quiet --no-align --tuples-only "$@"
}
psql_db() {
  _db="$1"; shift
  psql --host="$PGHOST" --port="$PGPORT" --username="$PGUSER" --dbname="$_db" \
    --set=ON_ERROR_STOP=1 --quiet --no-align --tuples-only "$@"
}

cleanup() {
  psql_admin --command="DROP DATABASE IF EXISTS \"$SRC_DB\" WITH (FORCE);" >/dev/null 2>&1 || true
  psql_admin --command="DROP DATABASE IF EXISTS \"$DST_DB\" WITH (FORCE);" >/dev/null 2>&1 || true
  rm -rf "$WORK_DIR" 2>/dev/null || true
}
trap cleanup EXIT

fail() { echo "selfcheck: FAIL - $1" >&2; exit 1; }

echo "=== ALKAROS backup/restore round-trip self-check ==="
echo "server: ${PGUSER}@${PGHOST}:${PGPORT}  pg_dump: $(pg_dump --version | awk '{print $NF}')"
date -u +"started: %Y-%m-%dT%H:%M:%SZ"

mkdir -p "$WORK_DIR"
cleanup

# ---------------------------------------------------------------------------
# 1. disposable source database + seeded verification table
# ---------------------------------------------------------------------------
psql_admin --command="CREATE DATABASE \"$SRC_DB\";"
psql_db "$SRC_DB" <<'SQL'
CREATE SCHEMA recovery_selfcheck;
CREATE TABLE recovery_selfcheck.verification (
    id     integer PRIMARY KEY,
    label  text        NOT NULL,
    amount numeric(12,2) NOT NULL,
    noted  timestamptz NOT NULL DEFAULT now()
);
INSERT INTO recovery_selfcheck.verification (id, label, amount)
SELECT g, 'row-' || g, (g * 3.5)::numeric(12,2)
FROM generate_series(1, 500) AS g;
SQL

CHECKSUM_SQL="SELECT md5(coalesce(string_agg(id::text || ':' || label || ':' || amount::text, '|' ORDER BY id), '')) FROM recovery_selfcheck.verification;"
SRC_COUNT="$(psql_db "$SRC_DB" --command="SELECT count(*) FROM recovery_selfcheck.verification;")"
SRC_CHECKSUM="$(psql_db "$SRC_DB" --command="$CHECKSUM_SQL")"
echo "source: rows=${SRC_COUNT} data_md5=${SRC_CHECKSUM}"

# ---------------------------------------------------------------------------
# 2. backup
# ---------------------------------------------------------------------------
echo "--- backup ---"
ALKAROS_DB_NAME="$SRC_DB" sh "${HERE}/backup.sh"
ARTIFACT="$(ls -1t "${WORK_DIR}"/alkaros_${SRC_DB}_*.dump | head -1)"
[ -f "$ARTIFACT" ] || fail "backup produced no artifact"
[ -f "${ARTIFACT}.sha256" ] || fail "backup produced no .sha256 sidecar"
BACKUP_SECONDS="$(date +%s)"

# ---------------------------------------------------------------------------
# 3. NEGATIVE: corrupted artifact must be refused
# ---------------------------------------------------------------------------
echo "--- negative: corrupted artifact ---"
CORRUPT="${WORK_DIR}/corrupted.dump"
cp "$ARTIFACT" "$CORRUPT"
cp "${ARTIFACT}.sha256" "${CORRUPT}.sha256"
sed -i "s|$(basename "$ARTIFACT")|corrupted.dump|" "${CORRUPT}.sha256"
# flip the middle byte
CORRUPT_SIZE="$(wc -c < "$CORRUPT")"
MID="$((CORRUPT_SIZE / 2))"
printf '\xFF' | dd of="$CORRUPT" bs=1 seek="$MID" count=1 conv=notrunc status=none
set +e
ALKAROS_RESTORE_DB="$DST_DB" sh "${HERE}/restore.sh" "$CORRUPT" "$DST_DB"
RC=$?
set -e
[ "$RC" -eq 4 ] || fail "restore accepted a corrupted artifact (exit ${RC}, expected 4)"
psql_admin --command="SELECT 1 FROM pg_database WHERE datname = '${DST_DB}';" | grep -q 1 \
  && fail "restore created the target database despite checksum mismatch"
echo "negative: corrupted artifact correctly refused (exit 4, no target database)"

# ---------------------------------------------------------------------------
# 4. POSITIVE: intact artifact restores into a fresh database
# ---------------------------------------------------------------------------
echo "--- positive: clean restore ---"
RESTORE_START="$(date +%s)"
ALKAROS_RESTORE_DB="$DST_DB" sh "${HERE}/restore.sh" "$ARTIFACT" "$DST_DB"
RESTORE_END="$(date +%s)"

# ---------------------------------------------------------------------------
# 5. compare
# ---------------------------------------------------------------------------
DST_COUNT="$(psql_db "$DST_DB" --command="SELECT count(*) FROM recovery_selfcheck.verification;")"
DST_CHECKSUM="$(psql_db "$DST_DB" --command="$CHECKSUM_SQL")"
echo "target: rows=${DST_COUNT} data_md5=${DST_CHECKSUM}"

[ "$SRC_COUNT" = "$DST_COUNT" ] || fail "row count mismatch src=${SRC_COUNT} dst=${DST_COUNT}"
[ "$SRC_CHECKSUM" = "$DST_CHECKSUM" ] || fail "data checksum mismatch src=${SRC_CHECKSUM} dst=${DST_CHECKSUM}"

ARTIFACT_SHA="$(cut -d' ' -f1 < "${ARTIFACT}.sha256")"
echo
echo "=== PASS ==="
echo "artifact         : $(basename "$ARTIFACT")"
echo "artifact sha256  : ${ARTIFACT_SHA}"
echo "artifact bytes   : $(wc -c < "$ARTIFACT")"
echo "rows             : ${SRC_COUNT} (src) == ${DST_COUNT} (dst)"
echo "data md5         : ${SRC_CHECKSUM} (src) == ${DST_CHECKSUM} (dst)"
echo "restore seconds  : $((RESTORE_END - RESTORE_START))"
echo "corrupted artifact: refused with exit 4, no target database created"
date -u +"finished: %Y-%m-%dT%H:%M:%SZ"
