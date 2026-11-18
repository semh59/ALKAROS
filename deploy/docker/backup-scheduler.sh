#!/bin/sh
# ALKAROS automatic backup scheduler (V1-RMD-1167).
#
# Runs for the life of the stack (compose service `backup-scheduler`). Every tick it takes whatever is due:
#   - a logical dump (backup.sh) every ALKAROS_LOGICAL_BACKUP_MINUTES (default 60),
#   - a physical base backup (basebackup.sh) every ALKAROS_BASE_BACKUP_HOURS (default 24),
# verifies the result, records one operations.backups row per attempt (read by the DomainDetectors alarms),
# prunes old artifacts and, when ALKAROS_OFFSITE_COPY_ENABLED is set, copies dumps, base backups and WAL to /offsite-copy.
#
# Retention (one number each, see docs/recovery/rpo-rto-targets.md section 3):
#   - ALKAROS_BASE_BACKUP_KEEP (default 2, minimum 2): the newest N verified base backups. One base can turn out bad,
#     so the one before it must stay restorable.
#   - WAL: everything older than the START WAL file written inside the oldest kept base backup's own backup_label is
#     removed with pg_archivecleanup, and only after the newest base backup verified. Unreadable label: nothing is removed.
#   - Dumps: all of the last ALKAROS_DUMP_KEEP_HOURS (48), then the first dump of each UTC day up to
#     ALKAROS_DUMP_KEEP_DAILY_DAYS (30).
#   - The newest dump is never pruned, so a long run of failures cannot delete the last good copy.
#   - ALKAROS_WAL_ARCHIVE_MAX_GB (10) is not enforced here; it is recorded so the alarm can compare the real size.
# A failed attempt is retried after ALKAROS_BACKUP_RETRY_MINUTES (30), not on every tick. The off-machine state
# (offsite=...) is stamped on the hourly logical row; local WAL is pruned whether or not the target was reachable (the
# local disk comes first), so a target that stays unreachable for longer than the retention window leaves a gap in its
# WAL chain until the next base backup has been copied.
#
# Usage: backup-scheduler.sh [loop|once|prune]   (loop is the default; once exits non-zero if any attempt failed)
# Environment: ALKAROS_DB_HOST/PORT/USER/NAME, ALKAROS_BACKUP_DIR, ALKAROS_BASEBACKUP_DIR, ALKAROS_WAL_ARCHIVE_DIR,
#   ALKAROS_OFFSITE_COPY_DIR, ALKAROS_BACKUP_TICK_SECONDS (300). Password: /run/secrets/db_password or ALKAROS_DB_PASSWORD.
set -u

HERE="$(cd "$(dirname "$0")" && pwd)"
PGHOST="${ALKAROS_DB_HOST:-postgres}"
PGPORT="${ALKAROS_DB_PORT:-5432}"
PGUSER="${ALKAROS_DB_USER:-alkaros}"
PGDATABASE="${ALKAROS_DB_NAME:-alkaros}"
BACKUP_DIR="${ALKAROS_BACKUP_DIR:-/backups}"
BASE_DIR="${ALKAROS_BASEBACKUP_DIR:-$BACKUP_DIR/base}"
WAL_DIR="${ALKAROS_WAL_ARCHIVE_DIR:-/wal-archive}"
OFFSITE_DIR="${ALKAROS_OFFSITE_COPY_DIR:-/offsite-copy}"
OFFSITE_ENABLED="${ALKAROS_OFFSITE_COPY_ENABLED:-}"
TICK="${ALKAROS_BACKUP_TICK_SECONDS:-300}"
LOGICAL_MINUTES="${ALKAROS_LOGICAL_BACKUP_MINUTES:-60}"
BASE_HOURS="${ALKAROS_BASE_BACKUP_HOURS:-24}"
KEEP="${ALKAROS_BASE_BACKUP_KEEP:-2}"
DUMP_KEEP_HOURS="${ALKAROS_DUMP_KEEP_HOURS:-48}"
DUMP_KEEP_DAILY_DAYS="${ALKAROS_DUMP_KEEP_DAILY_DAYS:-30}"
WAL_MAX_GB="${ALKAROS_WAL_ARCHIVE_MAX_GB:-10}"
RETRY_MINUTES="${ALKAROS_BACKUP_RETRY_MINUTES:-30}"

log() { echo "backup-scheduler: $*"; }

need_int() {
  case "$2" in ''|*[!0-9]*) echo "backup-scheduler: $1 must be a whole number, got '$2'" >&2; exit 2 ;; esac
  [ "$2" -ge "$3" ] || { echo "backup-scheduler: $1 must be at least $3, got $2" >&2; exit 2; }
}
need_int ALKAROS_BACKUP_TICK_SECONDS "$TICK" 1
need_int ALKAROS_LOGICAL_BACKUP_MINUTES "$LOGICAL_MINUTES" 0
need_int ALKAROS_BASE_BACKUP_HOURS "$BASE_HOURS" 0
need_int ALKAROS_BASE_BACKUP_KEEP "$KEEP" 2
need_int ALKAROS_DUMP_KEEP_HOURS "$DUMP_KEEP_HOURS" 1
need_int ALKAROS_DUMP_KEEP_DAILY_DAYS "$DUMP_KEEP_DAILY_DAYS" 1
need_int ALKAROS_WAL_ARCHIVE_MAX_GB "$WAL_MAX_GB" 1
need_int ALKAROS_BACKUP_RETRY_MINUTES "$RETRY_MINUTES" 0

if [ -f /run/secrets/db_password ]; then
  PGPASSWORD="$(tr -d '\r\n' < /run/secrets/db_password)"
elif [ -n "${ALKAROS_DB_PASSWORD:-}" ]; then
  PGPASSWORD="$ALKAROS_DB_PASSWORD"
else
  echo "backup-scheduler: no database password (mount /run/secrets/db_password or set ALKAROS_DB_PASSWORD)" >&2
  exit 2
fi
export PGHOST PGPORT PGUSER PGDATABASE PGPASSWORD
export ALKAROS_DB_HOST="$PGHOST" ALKAROS_DB_PORT="$PGPORT" ALKAROS_DB_USER="$PGUSER" ALKAROS_DB_NAME="$PGDATABASE"
export ALKAROS_BACKUP_DIR="$BACKUP_DIR" ALKAROS_BASEBACKUP_DIR="$BASE_DIR"

# Epoch seconds of the UTC stamp (YYYYMMDDTHHMMSSZ) inside an artifact name; fails when the name has no valid stamp.
stamp_epoch() {
  s="$(printf '%s' "$1" | sed -n 's/.*\([0-9]\{4\}\)\([0-9]\{2\}\)\([0-9]\{2\}\)T\([0-9]\{2\}\)\([0-9]\{2\}\)\([0-9]\{2\}\)Z.*/\1-\2-\3 \4:\5:\6/p')"
  [ -n "$s" ] && date -u -d "$s" +%s 2>/dev/null
}

# Prints the newest stamp epoch among the dumps that have a checksum sidecar (0 when none).
newest_dump_epoch() {
  newest=0
  for f in "$BACKUP_DIR"/alkaros_"$PGDATABASE"_*.dump; do
    [ -f "$f.sha256" ] || continue
    e="$(stamp_epoch "$f")" || continue
    [ "$e" -gt "$newest" ] && newest="$e"
  done
  echo "$newest"
}

# Base backup directory names that carry a checksum file (a copy in progress ends in .tmp), oldest first.
valid_bases() {
  for d in "$1"/alkaros_base_*; do
    case "$d" in *.tmp) continue ;; esac
    [ -f "$d/SHA256SUMS" ] && basename "$d"
  done | sort
}

newest_base_epoch() {
  stamp_epoch "$(valid_bases "$BASE_DIR" | tail -n 1)" || echo 0
}

# Leftovers of a crashed run: a dump without checksum sidecar or a base backup directory without SHA256SUMS, older than an hour
# (a younger one may be a manual backup still running).
sweep_partials() {
  for partial in "$BACKUP_DIR"/alkaros_"$PGDATABASE"_*.dump; do
    [ -f "$partial" ] && [ ! -f "$partial.sha256" ] && [ -n "$(find "$partial" -mmin +60)" ] && rm -f "$partial"
  done
  for partial in "$BASE_DIR"/alkaros_base_*; do
    [ -d "$partial" ] && [ ! -f "$partial/SHA256SUMS" ] && [ -z "$(find "$partial" -mmin -60)" ] && rm -rf "$partial"
  done
}

# START WAL file named inside a base backup's backup_label (the first entry of the tar).
base_start_wal() {
  tar -xzOf "$1/base.tar.gz" --occurrence=1 backup_label 2>/dev/null \
    | sed -n 's/^START WAL LOCATION: .*(file \([0-9A-F]\{24\}\))$/\1/p'
}

verify_dump() {
  ( cd "$BACKUP_DIR" && sha256sum -c "$(basename "$1").sha256" ) >/dev/null 2>&1 || return 1
  [ "$(pg_restore --list "$1" 2>/dev/null | grep -c -v '^;')" -gt 0 ]
}

verify_base() {
  ( cd "$1" && sha256sum -c SHA256SUMS ) >/dev/null 2>&1 || return 1
  gzip -t "$1/base.tar.gz" 2>/dev/null || return 1
  [ -n "$(base_start_wal "$1")" ]
}

# Keeps the newest $KEEP valid base backups under $1 and removes the WAL in $2 that none of them needs.
prune_bases_and_wal() {
  bases="$1"; wal="$2"
  count="$(valid_bases "$bases" | wc -l)"
  [ "$count" -gt 0 ] || return 0
  if [ "$count" -gt "$KEEP" ]; then
    valid_bases "$bases" | head -n "$((count - KEEP))" | while read -r old; do
      log "removing old base backup $old"
      rm -rf "${bases:?}/$old"
    done
  fi
  oldest_kept="$(valid_bases "$bases" | head -n 1)"
  start_wal="$(base_start_wal "$bases/$oldest_kept")"
  if [ -z "$start_wal" ]; then
    log "cannot read backup_label of $oldest_kept; WAL in $wal left untouched"
    return 1
  fi
  pg_archivecleanup -b "$wal" "$start_wal" || { log "pg_archivecleanup failed in $wal"; return 1; }
}

# Dumps under $1: everything of the last DUMP_KEEP_HOURS, then the first dump of each UTC day up to DUMP_KEEP_DAILY_DAYS.
# The newest dump always stays; a name without a valid stamp is left alone.
prune_dumps() {
  now="$(date -u +%s)"; last_day=""
  newest="$(for f in "$1"/alkaros_"$PGDATABASE"_*.dump; do [ -f "$f.sha256" ] && echo "$f"; done | sort | tail -n 1)"
  for f in "$1"/alkaros_"$PGDATABASE"_*.dump; do
    [ -f "$f.sha256" ] && [ "$f" != "$newest" ] || continue
    e="$(stamp_epoch "$f")" || continue
    age=$((now - e))
    day="$(basename "$f" | sed -n 's/.*_\([0-9]\{8\}\)T.*/\1/p')"
    if [ "$age" -ge $((DUMP_KEEP_HOURS * 3600)) ] \
      && { [ "$age" -ge $((DUMP_KEEP_DAILY_DAYS * 86400)) ] || [ "$day" = "$last_day" ]; }; then
      log "removing old dump $(basename "$f")"
      rm -f "$f" "$f.sha256"
    fi
    last_day="$day"
  done
}

# Copies what the target lacks, verifying each copy; sets OFFSITE_STATE to disabled, copied or failed.
sync_offsite() {
  if [ -z "$OFFSITE_ENABLED" ]; then OFFSITE_STATE=disabled; return 0; fi
  OFFSITE_STATE=copied
  if ! mkdir -p "$OFFSITE_DIR/base" "$OFFSITE_DIR/wal" || ! touch "$OFFSITE_DIR/.write-test" || ! rm -f "$OFFSITE_DIR/.write-test"; then
    log "off-machine target $OFFSITE_DIR is not writable"; OFFSITE_STATE=failed; return 1
  fi
  for f in "$BACKUP_DIR"/alkaros_"$PGDATABASE"_*.dump; do
    [ -f "$f.sha256" ] || continue
    n="$(basename "$f")"
    [ -f "$OFFSITE_DIR/$n" ] && continue
    { cp "$f" "$OFFSITE_DIR/$n.tmp" && cmp -s "$f" "$OFFSITE_DIR/$n.tmp" && cp "$f.sha256" "$OFFSITE_DIR/$n.sha256" \
        && mv "$OFFSITE_DIR/$n.tmp" "$OFFSITE_DIR/$n"; } || { log "copy of $n failed"; rm -f "$OFFSITE_DIR/$n.tmp"; OFFSITE_STATE=failed; }
  done
  for b in $(valid_bases "$BASE_DIR"); do
    [ -d "$OFFSITE_DIR/base/$b" ] && continue
    rm -rf "$OFFSITE_DIR/base/$b.tmp"
    { cp -R "$BASE_DIR/$b" "$OFFSITE_DIR/base/$b.tmp" && ( cd "$OFFSITE_DIR/base/$b.tmp" && sha256sum -c SHA256SUMS ) >/dev/null 2>&1 \
        && mv "$OFFSITE_DIR/base/$b.tmp" "$OFFSITE_DIR/base/$b"; } || { log "copy of $b failed"; rm -rf "$OFFSITE_DIR/base/$b.tmp"; OFFSITE_STATE=failed; }
  done
  for f in "$WAL_DIR"/*; do
    n="$(basename "$f")"
    case "$n" in '*'|*.tmp) continue ;; esac
    [ -e "$OFFSITE_DIR/wal/$n" ] && continue
    { cp "$f" "$OFFSITE_DIR/wal/$n.tmp" && cmp -s "$f" "$OFFSITE_DIR/wal/$n.tmp" && mv "$OFFSITE_DIR/wal/$n.tmp" "$OFFSITE_DIR/wal/$n"; } \
      || { log "copy of WAL $n failed"; rm -f "$OFFSITE_DIR/wal/$n.tmp"; OFFSITE_STATE=failed; }
  done
  prune_dumps "$OFFSITE_DIR"
  prune_bases_and_wal "$OFFSITE_DIR/base" "$OFFSITE_DIR/wal" || OFFSITE_STATE=failed
  [ "$OFFSITE_STATE" = copied ]
}

# record KIND STATUS PATH SIZE SHA256 ERROR STARTED_EPOCH: one operations.backups row (read by the DomainDetectors).
record() {
  psql -X -q -v ON_ERROR_STOP=1 -v kind="$1" -v status="$2" -v path="$3" -v size="${4:-0}" -v sum="${5:-}" -v err="${6:-}" \
    -v started="$7" -v offsite="$OFFSITE_STATE" -v walbytes="$WAL_BYTES" -v walmax="$((WAL_MAX_GB * 1073741824))" <<'SQL'
INSERT INTO operations.backups (backup_id, backup_type, file_path, file_size_bytes, checksum_sha256, status, error_message,
                                started_at, completed_at, metadata)
VALUES (gen_random_uuid(), 'Full', :'path', :size, :'sum', :'status', NULLIF(:'err', ''), to_timestamp(:started),
        CASE WHEN :'status' = 'Completed' THEN now() END,
        jsonb_build_object('kind', :'kind', 'offsite', :'offsite', 'walArchiveBytes', :walbytes::bigint,
                           'walArchiveLimitBytes', :walmax::bigint));
SQL
}

# Each run_* sets <P>_STATUS/FILE/SIZE/SUM/ERR for record(); it returns non-zero when the attempt failed.
run_logical() {
  L_START="$(date -u +%s)"; L_FILE=""; L_SIZE=0; L_SUM=""; L_ERR=""; L_STATUS=Failed
  out="$(sh "$HERE/backup.sh" 2>&1)"; rc=$?
  printf '%s\n' "$out" | tail -n 3
  name="$(printf '%s\n' "$out" | sed -n 's/^backup: ok artifact=\([^ ]*\).*/\1/p')"
  if [ "$rc" -ne 0 ] || [ -z "$name" ]; then
    L_ERR="pg_dump failed (exit $rc)"
    target="$(printf '%s\n' "$out" | sed -n 's/^backup: pg_dump .* -> //p')"
    [ -n "$target" ] && [ ! -f "$target.sha256" ] && rm -f "$target"
    return 1
  fi
  L_FILE="$BACKUP_DIR/$name"
  if ! verify_dump "$L_FILE"; then
    L_ERR="dump verification failed (checksum or pg_restore --list)"
    rm -f "$L_FILE" "$L_FILE.sha256"; return 1
  fi
  L_SIZE="$(wc -c < "$L_FILE")"; L_SUM="$(cut -d' ' -f1 < "$L_FILE.sha256")"; L_STATUS=Completed
}

run_base() {
  B_START="$(date -u +%s)"; B_FILE=""; B_SIZE=0; B_SUM=""; B_ERR=""; B_STATUS=Failed
  out="$(sh "$HERE/basebackup.sh" 2>&1)"; rc=$?
  printf '%s\n' "$out" | tail -n 3
  name="$(printf '%s\n' "$out" | sed -n 's/^basebackup: ok dir=\([^ ]*\).*/\1/p')"
  if [ "$rc" -ne 0 ] || [ -z "$name" ]; then
    B_ERR="pg_basebackup failed (exit $rc)"
    target="$(printf '%s\n' "$out" | sed -n 's/^basebackup: pg_basebackup .* -> //p')"
    [ -n "$target" ] && [ ! -f "$target/SHA256SUMS" ] && rm -rf "$target"
    return 1
  fi
  B_FILE="$BASE_DIR/$name"
  if ! verify_base "$B_FILE"; then
    B_ERR="base backup verification failed (checksum, gzip or backup_label)"
    rm -rf "$B_FILE"; return 1
  fi
  B_SIZE="$(du -sb "$B_FILE" | cut -f1)"; B_SUM="$(cut -d' ' -f1 < "$B_FILE/SHA256SUMS" | head -n 1)"; B_STATUS=Completed
  prune_bases_and_wal "$BASE_DIR" "$WAL_DIR" || { B_ERR="WAL pruning failed (see the container log)"; B_STATUS=Failed; return 1; }
}

# attempt KIND FUNCTION: runs one backup; a failure is remembered so the next try waits RETRY_MINUTES.
attempt() {
  "$2" && rm -f "$BACKUP_DIR/.last-failure-$1" && return 0
  date -u +%s > "$BACKUP_DIR/.last-failure-$1"
  return 1
}

# retry_pending KIND: true while the last attempt of that kind failed less than RETRY_MINUTES ago.
retry_pending() {
  last="$(cat "$BACKUP_DIR/.last-failure-$1" 2>/dev/null)"
  case "$last" in ''|*[!0-9]*) return 1 ;; esac
  [ $((now - last)) -lt $((RETRY_MINUTES * 60)) ]
}

cycle() {
  failed=0; ran_logical=0; ran_base=0; now="$(date -u +%s)"
  sweep_partials
  if [ $((now - $(newest_dump_epoch))) -ge $((LOGICAL_MINUTES * 60)) ] && ! retry_pending logical; then
    ran_logical=1; attempt logical run_logical || failed=1
  fi
  if [ $((now - $(newest_base_epoch))) -ge $((BASE_HOURS * 3600)) ] && ! retry_pending base; then
    ran_base=1; attempt base run_base || failed=1
  fi
  prune_dumps "$BACKUP_DIR"
  WAL_BYTES="$(du -sb "$WAL_DIR" 2>/dev/null | cut -f1)"; WAL_BYTES="${WAL_BYTES:-0}"
  sync_offsite || failed=1
  if [ "$ran_logical" -eq 1 ]; then
    record logical "$L_STATUS" "${L_FILE:-$BACKUP_DIR}" "$L_SIZE" "$L_SUM" "$L_ERR" "$L_START" || { log "could not record the logical backup"; failed=1; }
  fi
  if [ "$ran_base" -eq 1 ]; then
    record base "$B_STATUS" "${B_FILE:-$BASE_DIR}" "$B_SIZE" "$B_SUM" "$B_ERR" "$B_START" || { log "could not record the base backup"; failed=1; }
  fi
  return "$failed"
}

case "${1:-loop}" in
  once) cycle; exit $? ;;
  prune)
    sweep_partials; prune_dumps "$BACKUP_DIR"; prune_bases_and_wal "$BASE_DIR" "$WAL_DIR"; rc=$?
    [ -z "$OFFSITE_ENABLED" ] || { prune_dumps "$OFFSITE_DIR"; prune_bases_and_wal "$OFFSITE_DIR/base" "$OFFSITE_DIR/wal" || rc=1; }
    exit "$rc" ;;
  loop)
    trap 'log "stopping"; exit 0' TERM INT
    log "started: logical every ${LOGICAL_MINUTES} min, base every ${BASE_HOURS} h, keep ${KEEP} base backups, off-machine copy: ${OFFSITE_ENABLED:-off}"
    while :; do
      cycle || log "this cycle had failures; retrying in ${TICK} s"
      sleep "$TICK" & wait $!
    done ;;
  *) echo "usage: backup-scheduler.sh [loop|once|prune]" >&2; exit 2 ;;
esac
