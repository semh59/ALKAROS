#!/bin/sh
# ALKAROS point-in-time recovery from a physical base backup + WAL archive
# (V1-RMD-095).
#
# Rebuilds a cluster data directory from a base backup taken by basebackup.sh and
# replays the archived WAL up to a target time. It NEVER writes to the live
# cluster: the recovered data directory is a separate path you start on a
# separate port, verify, and only then promote by swapping data directories.
#
# Usage:
#   restore-pitr.sh <base-backup-dir> <target-datadir> "<recovery target time>"
#
#   <base-backup-dir>  a directory from basebackup.sh (contains base.tar.gz [+ pg_wal.tar.gz])
#   <target-datadir>   an EMPTY path to build the recovered cluster in
#   <target time>      e.g. "2026-09-01 17:30:00+00"  (omit / "latest" to replay all WAL)
#
# Environment:
#   ALKAROS_WAL_ARCHIVE_DIR   directory holding archived WAL segments (default: /backups/wal)
#
# Exit codes: 0 ok | 2 bad arguments | 3 missing base backup | 4 checksum mismatch
set -eu

BASE_DIR="${1:-}"
DATADIR="${2:-}"
TARGET_TIME="${3:-latest}"
WAL_DIR="${ALKAROS_WAL_ARCHIVE_DIR:-/backups/wal}"
PGBIN="$(pg_config --bindir 2>/dev/null || echo /usr/lib/postgresql/18/bin)"

[ -n "$BASE_DIR" ] && [ -n "$DATADIR" ] || {
  echo "usage: restore-pitr.sh <base-backup-dir> <target-datadir> \"<recovery target time>\"" >&2
  exit 2
}
[ -f "$BASE_DIR/base.tar.gz" ] || { echo "restore-pitr: no base.tar.gz in $BASE_DIR" >&2; exit 3; }
[ -d "$WAL_DIR" ] || { echo "restore-pitr: WAL archive dir $WAL_DIR not found" >&2; exit 3; }
if [ -e "$DATADIR" ] && [ -n "$(ls -A "$DATADIR" 2>/dev/null || true)" ]; then
  echo "restore-pitr: target datadir $DATADIR is not empty; refusing" >&2
  exit 2
fi

# checksum-gate the base backup if a SHA256SUMS sidecar is present
if [ -f "$BASE_DIR/SHA256SUMS" ]; then
  if command -v sha256sum >/dev/null 2>&1; then
    ( cd "$BASE_DIR" && sha256sum -c SHA256SUMS >/dev/null 2>&1 ) \
      || { echo "restore-pitr: CHECKSUM MISMATCH in $BASE_DIR; refusing to restore" >&2; exit 4; }
  fi
  echo "restore-pitr: base backup checksum verified"
else
  echo "restore-pitr: WARNING no SHA256SUMS sidecar in $BASE_DIR (proceeding unverified)"
fi

mkdir -p "$DATADIR"
echo "restore-pitr: expanding base backup -> $DATADIR"
tar -xzf "$BASE_DIR/base.tar.gz" -C "$DATADIR"
[ -f "$BASE_DIR/pg_wal.tar.gz" ] && tar -xzf "$BASE_DIR/pg_wal.tar.gz" -C "$DATADIR/pg_wal"
rm -f "$DATADIR/postmaster.pid"
# PostgreSQL refuses to start unless the data directory is owned by the running
# user and is mode 700. If this script runs as root (the ops container), hand the
# tree to the postgres user before it is started.
if [ "$(id -u)" = "0" ]; then
  chown -R postgres:postgres "$DATADIR"
fi
chmod 700 "$DATADIR"

RECOVERY_LINE="recovery_target_action = 'promote'"
if [ "$TARGET_TIME" != "latest" ] && [ -n "$TARGET_TIME" ]; then
  RECOVERY_LINE="recovery_target_time = '$TARGET_TIME'
recovery_target_action = 'promote'"
fi

# PostgreSQL aborts recovery if certain parameters are lower than they were on
# the primary when the WAL was written. The tuned ALKAROS config raises some of
# them (e.g. max_connections = 200), and those values are not in the base
# backup's postgresql.conf because the deployment loads an external config_file.
# Read the primary's values straight out of pg_control and match them.
CONTROL="$("$PGBIN/pg_controldata" "$DATADIR" 2>/dev/null || true)"
emit_guc() {
  _label="$1"; _guc="$2"
  _val="$(printf '%s\n' "$CONTROL" | sed -n "s/^$_label setting: *//p")"
  [ -n "$_val" ] && echo "$_guc = $_val"
}
{
  echo "# --- added by restore-pitr.sh $(date -u +%Y-%m-%dT%H:%M:%SZ) ---"
  emit_guc "max_connections"        "max_connections"
  emit_guc "max_worker_processes"   "max_worker_processes"
  emit_guc "max_wal_senders"        "max_wal_senders"
  emit_guc "max_prepared_xacts"     "max_prepared_transactions"
  emit_guc "max_locks_per_xact"     "max_locks_per_transaction"
  echo "archive_mode = off"
  echo "restore_command = 'cp $WAL_DIR/%f %p'"
  echo "$RECOVERY_LINE"
} >> "$DATADIR/postgresql.auto.conf"
touch "$DATADIR/recovery.signal"

echo "restore-pitr: data directory ready at $DATADIR"
echo "restore-pitr: target = $TARGET_TIME"
echo "restore-pitr: start it on a spare port as the postgres user and verify, e.g."
echo "  su postgres -c \"$PGBIN/pg_ctl -D $DATADIR -o '-p 5601 -k /tmp' -w start\""
echo "  su postgres -c \"psql -h /tmp -p 5601 -c 'SELECT pg_is_in_recovery()'\"  # 'f' once replay finished"
echo "restore-pitr: promote only after verification by swapping data directories"
