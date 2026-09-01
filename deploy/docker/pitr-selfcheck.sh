#!/bin/sh
# ALKAROS point-in-time recovery (WAL archiving) self-check (V1-RMD-095).
#
# Proves the PITR path end to end inside a single disposable PostgreSQL 18
# container, WITHOUT touching any real database or the compose stack:
#
#   1. initdb a throwaway primary cluster with archive_mode=on
#   2. seed a baseline, then pg_basebackup it
#   3. write "keep" rows, force a WAL switch, capture the recovery target time
#   4. write "must-be-lost" rows after the target time, force another WAL switch
#   5. restore the base backup + replay WAL to the target time into a new datadir
#   6. assert the recovered cluster has the "keep" rows and NOT the later ones
#
# Exit 0 only when every assertion holds.
set -eu

ROOT="${ALKAROS_PITR_WORKDIR:-/tmp/alkaros-pitr}"
PRIMARY="$ROOT/primary"
BASE="$ROOT/base"
WAL="$ROOT/wal-archive"
RESTORED="$ROOT/restored"
PGBIN="$(pg_config --bindir 2>/dev/null || echo /usr/lib/postgresql/18/bin)"
PRIMARY_PORT=5599
RESTORE_PORT=5600
export PGUSER=postgres

log() { echo "pitr-selfcheck: $*"; }
fail() { echo "pitr-selfcheck: FAIL - $1" >&2; exit 1; }

cleanup() {
  "$PGBIN/pg_ctl" -D "$PRIMARY" -m immediate stop >/dev/null 2>&1 || true
  "$PGBIN/pg_ctl" -D "$RESTORED" -m immediate stop >/dev/null 2>&1 || true
  rm -rf "$ROOT" 2>/dev/null || true
}
trap cleanup EXIT

echo "=== ALKAROS point-in-time recovery self-check ==="
echo "pg_basebackup: $("$PGBIN/pg_basebackup" --version | awk '{print $NF}')"
date -u +"started: %Y-%m-%dT%H:%M:%SZ"

cleanup
mkdir -p "$PRIMARY" "$WAL"

# ---------------------------------------------------------------------------
# 1. throwaway primary with WAL archiving
# ---------------------------------------------------------------------------
"$PGBIN/initdb" -D "$PRIMARY" --username=postgres --auth=trust --encoding=UTF8 >/dev/null
cat >> "$PRIMARY/postgresql.conf" <<CONF
port = $PRIMARY_PORT
listen_addresses = ''
unix_socket_directories = '$ROOT'
archive_mode = on
archive_command = 'test ! -f $WAL/%f && cp %p $WAL/%f'
archive_timeout = 60
wal_level = replica
CONF
"$PGBIN/pg_ctl" -D "$PRIMARY" -w -o "-k '$ROOT'" start >/dev/null
PSQL="$PGBIN/psql -h $ROOT -p $PRIMARY_PORT -v ON_ERROR_STOP=1 -qtA"

$PSQL -c "CREATE TABLE recovery_pitr (id int PRIMARY KEY, phase text NOT NULL, at timestamptz NOT NULL DEFAULT clock_timestamp());"
$PSQL -c "INSERT INTO recovery_pitr (id, phase) SELECT g, 'baseline' FROM generate_series(1,100) g;"
log "baseline rows=$($PSQL -c 'SELECT count(*) FROM recovery_pitr;')"

# ---------------------------------------------------------------------------
# 2. base backup
# ---------------------------------------------------------------------------
"$PGBIN/pg_basebackup" -h "$ROOT" -p "$PRIMARY_PORT" -D "$BASE" -Fp -Xs -P >/dev/null 2>&1
[ -f "$BASE/PG_VERSION" ] || fail "pg_basebackup produced no cluster"
log "base backup ok bytes=$(du -sb "$BASE" | cut -f1)"

# ---------------------------------------------------------------------------
# 3. "keep" rows, WAL switch, capture the recovery target
# ---------------------------------------------------------------------------
$PSQL -c "INSERT INTO recovery_pitr (id, phase) SELECT g, 'keep' FROM generate_series(101,150) g;"
$PSQL -c "SELECT pg_switch_wal();" >/dev/null
TARGET="$($PSQL -c "SELECT now();")"
log "recovery target time = $TARGET"
sleep 2

# ---------------------------------------------------------------------------
# 4. "must-be-lost" rows after the target, WAL switch, archive them
# ---------------------------------------------------------------------------
$PSQL -c "INSERT INTO recovery_pitr (id, phase) SELECT g, 'lost' FROM generate_series(151,200) g;"
$PSQL -c "SELECT pg_switch_wal();" >/dev/null
$PSQL -c "CHECKPOINT;" >/dev/null
sleep 2
log "primary rows before stop=$($PSQL -c 'SELECT count(*) FROM recovery_pitr;')"
"$PGBIN/pg_ctl" -D "$PRIMARY" -w -m fast stop >/dev/null

# ---------------------------------------------------------------------------
# 5. restore base + replay WAL to the target time
# ---------------------------------------------------------------------------
cp -r "$BASE" "$RESTORED"
rm -f "$RESTORED/postmaster.pid"
cat >> "$RESTORED/postgresql.conf" <<CONF
port = $RESTORE_PORT
listen_addresses = ''
unix_socket_directories = '$ROOT'
archive_mode = off
restore_command = 'cp $WAL/%f %p'
recovery_target_time = '$TARGET'
recovery_target_action = 'promote'
CONF
touch "$RESTORED/recovery.signal"
"$PGBIN/pg_ctl" -D "$RESTORED" -w -t 60 -o "-k '$ROOT'" start >/dev/null
RSQL="$PGBIN/psql -h $ROOT -p $RESTORE_PORT -v ON_ERROR_STOP=1 -qtA"

# wait for recovery to finish
i=0
while [ "$($RSQL -c 'SELECT pg_is_in_recovery();')" = "t" ]; do
  i=$((i + 1)); [ "$i" -gt 60 ] && fail "recovery did not complete"; sleep 1
done

TOTAL="$($RSQL -c 'SELECT count(*) FROM recovery_pitr;')"
MAXID="$($RSQL -c 'SELECT max(id) FROM recovery_pitr;')"
LOST="$($RSQL -c "SELECT count(*) FROM recovery_pitr WHERE phase = 'lost';")"
echo "recovered: rows=$TOTAL max_id=$MAXID lost_phase_rows=$LOST"

[ "$TOTAL" = "150" ] || fail "expected 150 rows after PITR, got $TOTAL"
[ "$MAXID" = "150" ] || fail "expected max id 150 after PITR, got $MAXID"
[ "$LOST" = "0" ] || fail "rows written after the target time survived PITR ($LOST)"

echo
echo "=== PASS ==="
echo "baseline+keep rows recovered : 150"
echo "post-target rows dropped      : 50 (ids 151-200 absent)"
echo "recovery target              : $TARGET"
date -u +"finished: %Y-%m-%dT%H:%M:%SZ"
