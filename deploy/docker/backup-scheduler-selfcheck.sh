#!/bin/sh
# ALKAROS backup scheduler self-check (V1-RMD-1167).
#
# Proves backup-scheduler.sh end to end inside one disposable PostgreSQL 18 container (no compose stack, no real
# database touched). Run as the postgres user, with the repository mounted read-only:
#   docker run --rm -u postgres -v <repo>:/repo:ro postgres:18 sh /repo/deploy/docker/backup-scheduler-selfcheck.sh
#
#   1. throwaway primary with WAL archiving and the operations.backups table from migration 029
#   2. three rounds (dump + base backup + off-machine copy): only 2 base backups remain, no WAL older than the oldest
#      kept base's START WAL file remains, recovery from that OLDEST kept base still replays to the newest row
#   3. failure paths: broken pg_restore -> Failed row + non-zero exit + artifact removed; unwritable target -> offsite
#      failed; no target -> offsite disabled; unverifiable base backup -> removed, nothing pruned; keep < 2 refused;
#      unreadable backup_label -> no WAL removed
#   4. dump retention: 48 h all, then first dump of each UTC day up to 30 days; newest never pruned; bad stamp ignored;
#      crashed partial swept, young partial kept; a failed attempt waits before it is retried
# Exit 0 only when every assertion holds.
set -eu

ROOT="${ALKAROS_SCHED_WORKDIR:-/tmp/alkaros-sched}"
HERE="$(cd "$(dirname "$0")" && pwd)"
REPO="$(cd "$HERE/../.." && pwd)"
SCHED="$HERE/backup-scheduler.sh"
PGBIN="$(pg_config --bindir 2>/dev/null || echo /usr/lib/postgresql/18/bin)"
SOCK="$ROOT/sock"; PRIMARY="$ROOT/primary"; BACKUPS="$ROOT/backups"; WAL="$ROOT/wal"; OFFSITE="$ROOT/offsite"
RESTORED="$ROOT/restored"

log() { echo "sched-selfcheck: $*"; }
fail() { echo "sched-selfcheck: FAIL - $1" >&2; exit 1; }
cleanup() {
  "$PGBIN/pg_ctl" -D "$PRIMARY" -m immediate stop >/dev/null 2>&1 || true
  "$PGBIN/pg_ctl" -D "$RESTORED" -m immediate stop >/dev/null 2>&1 || true
  chmod -R u+w "$ROOT" 2>/dev/null || true
  rm -rf "$ROOT" 2>/dev/null || true
}
trap cleanup EXIT
cleanup
mkdir -p "$SOCK" "$BACKUPS" "$WAL" "$OFFSITE"

"$PGBIN/initdb" -D "$PRIMARY" --username=alkaros --auth=trust --encoding=UTF8 >/dev/null
cat >> "$PRIMARY/postgresql.conf" <<CONF
port = 5601
listen_addresses = ''
unix_socket_directories = '$SOCK'
archive_mode = on
archive_command = 'test ! -f $WAL/%f && cp %p $WAL/%f.tmp && mv $WAL/%f.tmp $WAL/%f'
wal_level = replica
CONF
"$PGBIN/pg_ctl" -D "$PRIMARY" -w -o "-k '$SOCK'" start >/dev/null
export PGHOST="$SOCK" PGPORT=5601 PGUSER=alkaros
"$PGBIN/createdb" alkaros
q() { "$PGBIN/psql" -d alkaros -v ON_ERROR_STOP=1 -qtA -c "$1"; }
"$PGBIN/psql" -d alkaros -v ON_ERROR_STOP=1 -q -f "$REPO/database/migrations/V1/V1-OPS-002/029-backup-health.up.sql"
q "CREATE TABLE t (n int);"

export ALKAROS_DB_HOST="$SOCK" ALKAROS_DB_PORT=5601 ALKAROS_DB_USER=alkaros ALKAROS_DB_PASSWORD=unused
export ALKAROS_BACKUP_DIR="$BACKUPS" ALKAROS_WAL_ARCHIVE_DIR="$WAL" ALKAROS_OFFSITE_COPY_DIR="$OFFSITE"
export ALKAROS_OFFSITE_COPY_ENABLED=yes ALKAROS_LOGICAL_BACKUP_MINUTES=0 ALKAROS_BASE_BACKUP_HOURS=0 ALKAROS_BACKUP_RETRY_MINUTES=0
run_once() { sh "$SCHED" once >/dev/null 2>&1; }
newest_base() { ls "$BACKUPS/base" | sort | tail -n 1; }
start_wal_of() { tar -xzOf "$1/base.tar.gz" --occurrence=1 backup_label | sed -n 's/^START WAL LOCATION: .*(file \([0-9A-F]\{24\}\))$/\1/p'; }

# --- 2. three rounds ---------------------------------------------------------------------------------------------
for round in 1 2 3; do
  q "INSERT INTO t SELECT g FROM generate_series(1,100) g;"
  q "SELECT pg_switch_wal();" >/dev/null
  sleep 2
  run_once || fail "round $round: scheduler reported a failure"
done
q "INSERT INTO t SELECT g FROM generate_series(1,100) g;"
q "SELECT pg_switch_wal();" >/dev/null
sleep 3
EXPECTED="$(q 'SELECT count(*) FROM t;')"

for dir in "$BACKUPS/base" "$OFFSITE/base"; do
  n="$(ls "$dir" | wc -l)"
  [ "$n" = 2 ] || fail "expected 2 base backups in $dir, found $n"
done
OLDEST="$(ls "$BACKUPS/base" | sort | head -n 1)"
START="$(start_wal_of "$BACKUPS/base/$OLDEST")"
FIRST_WAL="$(ls "$WAL" | sort | head -n 1)"
[ "$FIRST_WAL" = "$START" ] || fail "first remaining WAL is $FIRST_WAL, expected the oldest kept base START WAL $START"
[ "$(ls "$OFFSITE/wal" | sort | head -n 1)" = "$START" ] || fail "off-machine WAL was not pruned to $START"
log "kept 2 base backups; oldest=$OLDEST start_wal=$START; wal files=$(ls "$WAL" | wc -l)"
[ "$(q "SELECT count(*) FROM operations.backups WHERE status='Completed' AND metadata->>'offsite'='copied';")" = 6 ] \
  || fail "expected 6 Completed rows (3 logical + 3 base) with offsite=copied"
[ "$(q "SELECT count(*) FROM operations.backups WHERE metadata->>'kind'='base' AND (metadata->>'walArchiveBytes')::bigint > 0;")" = 3 ] \
  || fail "base rows must carry walArchiveBytes"

# recovery from the OLDEST kept base must still reach the newest row
ALKAROS_WAL_ARCHIVE_DIR="$WAL" sh "$HERE/restore-pitr.sh" "$BACKUPS/base/$OLDEST" "$RESTORED" latest >/dev/null
"$PGBIN/pg_ctl" -D "$RESTORED" -w -t 120 -o "-k '$SOCK' -p 5602 -c listen_addresses='' -c archive_mode=off" start >/dev/null
i=0
while [ "$("$PGBIN/psql" -p 5602 -d alkaros -qtA -c 'SELECT pg_is_in_recovery();')" = t ]; do
  i=$((i + 1)); [ "$i" -gt 60 ] && fail "recovery did not finish"; sleep 1
done
GOT="$("$PGBIN/psql" -p 5602 -d alkaros -qtA -c 'SELECT count(*) FROM t;')"
[ "$GOT" = "$EXPECTED" ] || fail "recovery from the oldest kept base returned $GOT rows, expected $EXPECTED"
"$PGBIN/pg_ctl" -D "$RESTORED" -m fast stop >/dev/null
log "recovery from oldest kept base replayed $GOT of $EXPECTED rows"

# --- 3. failure paths ------------------------------------------------------------------------------------------------
mkdir -p "$ROOT/shim"; printf '#!/bin/sh\nexit 1\n' > "$ROOT/shim/pg_restore"; chmod +x "$ROOT/shim/pg_restore"
DUMPS_BEFORE="$(ls "$BACKUPS"/*.dump | wc -l)"
if PATH="$ROOT/shim:$PATH" ALKAROS_BASE_BACKUP_HOURS=9999 sh "$SCHED" once >/dev/null 2>&1; then fail "broken pg_restore must make the run fail"; fi
[ "$(q "SELECT count(*) FROM operations.backups WHERE status='Failed' AND metadata->>'kind'='logical' AND error_message LIKE '%verification%';")" = 1 ] \
  || fail "failed verification must write one Failed logical row"
PATH="$ROOT/shim:$PATH" ALKAROS_BACKUP_RETRY_MINUTES=30 ALKAROS_BASE_BACKUP_HOURS=9999 sh "$SCHED" once >/dev/null 2>&1   || fail "a failed attempt must wait before it is retried (the second run must not fail)"
[ "$(q "SELECT count(*) FROM operations.backups WHERE status='Failed' AND metadata->>'kind'='logical';")" = 1 ]   || fail "a failed attempt must not be retried before ALKAROS_BACKUP_RETRY_MINUTES"
[ "$(ls "$BACKUPS"/*.dump | wc -l)" = "$DUMPS_BEFORE" ] || fail "an unverified dump must be removed"

sleep 2; chmod a-w "$OFFSITE"
ALKAROS_BASE_BACKUP_HOURS=9999 sh "$SCHED" once >/dev/null 2>&1 && fail "an unwritable target must fail the run"
chmod u+w "$OFFSITE"
[ "$(q "SELECT metadata->>'offsite' FROM operations.backups WHERE metadata->>'kind'='logical' ORDER BY started_at DESC LIMIT 1;")" = failed ] \
  || fail "unwritable target must record offsite=failed"

sleep 2; ALKAROS_OFFSITE_COPY_ENABLED= ALKAROS_BASE_BACKUP_HOURS=9999 sh "$SCHED" once >/dev/null 2>&1 || fail "no target is not a failure"
[ "$(q "SELECT metadata->>'offsite' FROM operations.backups WHERE metadata->>'kind'='logical' ORDER BY started_at DESC LIMIT 1;")" = disabled ] \
  || fail "no target must record offsite=disabled"

echo 12abc > "$BACKUPS/.last-failure-logical"; sleep 2
ALKAROS_BACKUP_RETRY_MINUTES=30 ALKAROS_BASE_BACKUP_HOURS=9999 sh "$SCHED" once >/dev/null 2>&1 || fail "an unreadable failure marker must not stop the scheduler"
printf '#!/bin/sh
exit 1
' > "$ROOT/shim/gzip"; chmod +x "$ROOT/shim/gzip"
sleep 2
if PATH="$ROOT/shim:$PATH" ALKAROS_LOGICAL_BACKUP_MINUTES=9999 sh "$SCHED" once >/dev/null 2>&1; then fail "an unverifiable base backup must make the run fail"; fi
[ "$(q "SELECT count(*) FROM operations.backups WHERE status='Failed' AND metadata->>'kind'='base';")" = 1 ] || fail "failed base verification must write one Failed base row"
[ "$(ls "$BACKUPS/base" | wc -l)" = 2 ] && [ -d "$BACKUPS/base/$OLDEST" ] || fail "an unverified base must be removed and must not push out a kept base"
[ "$(ls "$WAL" | sort | head -n 1)" = "$START" ] || fail "no WAL may be pruned after an unverified base backup"
if ALKAROS_BASE_BACKUP_KEEP=1 sh "$SCHED" once >/dev/null 2>&1; then fail "keeping fewer than 2 base backups must be refused"; fi

WAL_BEFORE="$(ls "$WAL" | wc -l)"
OLDEST_TAR="$BACKUPS/base/$OLDEST/base.tar.gz"; cp "$OLDEST_TAR" "$ROOT/saved.tar.gz"; echo broken > "$OLDEST_TAR"
if OUT="$(ALKAROS_OFFSITE_COPY_ENABLED= sh "$SCHED" prune 2>&1)"; then fail "unreadable backup_label must make prune fail"; fi
case "$OUT" in *"cannot read backup_label"*) ;; *) fail "prune must say why it removed nothing" ;; esac
[ "$(ls "$WAL" | wc -l)" = "$WAL_BEFORE" ] || fail "no WAL may be removed when backup_label is unreadable"
cp "$ROOT/saved.tar.gz" "$OLDEST_TAR"

# --- 4. dump retention -----------------------------------------------------------------------------------------------
RET="$ROOT/retention"; mkdir -p "$RET"
mk() { n="alkaros_alkaros_$(date -u -d "$1" +%Y%m%dT%H%M%SZ).dump"; : > "$RET/$n"; : > "$RET/$n.sha256"; echo "$n"; }
DAY10="$(date -u -d '10 days ago' +%Y-%m-%d)"; DAY3="$(date -u -d '3 days ago' +%Y-%m-%d)"
GONE1="$(mk '40 days ago')"; KEEP_B="$(mk "$DAY10 01:00:00")"; GONE2="$(mk "$DAY10 13:00:00")"
KEEP_D="$(mk "$DAY3 05:00:00")"; GONE3="$(mk "$DAY3 15:00:00")"; KEEP_F="$(mk '1 hour ago')"
BOGUS="alkaros_alkaros_20201345T000000Z.dump"; : > "$RET/$BOGUS"; : > "$RET/$BOGUS.sha256"
OLD_PARTIAL="alkaros_alkaros_20260101T000000Z.dump"; : > "$RET/$OLD_PARTIAL"; touch -d '2 hours ago' "$RET/$OLD_PARTIAL"
NEW_PARTIAL="alkaros_alkaros_20260102T000000Z.dump"; : > "$RET/$NEW_PARTIAL"
mkdir -p "$RET/base/alkaros_base_20200101T000000Z" "$RET/base/alkaros_base_20200102T000000Z"
: > "$RET/base/alkaros_base_20200101T000000Z/base.tar.gz"; touch -d '3 hours ago' "$RET/base/alkaros_base_20200101T000000Z/base.tar.gz"
touch -d '3 hours ago' "$RET/base/alkaros_base_20200101T000000Z"
: > "$RET/base/alkaros_base_20200102T000000Z/base.tar.gz"; touch -d '3 hours ago' "$RET/base/alkaros_base_20200102T000000Z"
ALKAROS_BACKUP_DIR="$RET" ALKAROS_BASEBACKUP_DIR="$RET/base" ALKAROS_WAL_ARCHIVE_DIR="$RET/wal" ALKAROS_OFFSITE_COPY_ENABLED= \
  sh "$SCHED" prune >/dev/null 2>&1 || fail "dump prune failed"
for gone in "$GONE1" "$GONE2" "$GONE3"; do [ ! -e "$RET/$gone" ] && [ ! -e "$RET/$gone.sha256" ] || fail "$gone should have been pruned"; done
[ ! -e "$RET/$OLD_PARTIAL" ] || fail "a crashed run's partial dump must be swept"
[ ! -d "$RET/base/alkaros_base_20200101T000000Z" ] || fail "a crashed run's partial base backup must be swept"
[ -d "$RET/base/alkaros_base_20200102T000000Z" ] || fail "a base backup whose files are still being written must stay"
[ -e "$RET/$NEW_PARTIAL" ] || fail "a young partial dump may be a manual backup still running and must stay"
[ -e "$RET/$BOGUS" ] || fail "a name without a valid stamp must be left alone"
ONLY="$ROOT/only-one"; mkdir -p "$ONLY"; : > "$ONLY/alkaros_alkaros_20200101T000000Z.dump"; : > "$ONLY/alkaros_alkaros_20200101T000000Z.dump.sha256"
ALKAROS_BACKUP_DIR="$ONLY" ALKAROS_BASEBACKUP_DIR="$ONLY/base" ALKAROS_WAL_ARCHIVE_DIR="$ONLY/wal" ALKAROS_OFFSITE_COPY_ENABLED=   sh "$SCHED" prune >/dev/null 2>&1 || fail "prune of a single old dump failed"
[ -e "$ONLY/alkaros_alkaros_20200101T000000Z.dump" ] || fail "the newest dump must never be pruned"
for kept in "$KEEP_B" "$KEEP_D" "$KEEP_F"; do [ -e "$RET/$kept" ] && [ -e "$RET/$kept.sha256" ] || fail "$kept should have been kept"; done

echo
echo "=== PASS ==="
echo "base backups kept: 2 local + 2 off-machine; recovery from oldest kept base: $GOT rows"
echo "failure paths: verification, unwritable target, no target, unreadable backup_label"
echo "dump retention: 48 h all + first dump per UTC day up to 30 days"
