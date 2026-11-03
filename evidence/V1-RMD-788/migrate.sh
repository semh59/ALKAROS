#!/bin/sh
# forward (all migrations) -> 212 down -> 211 down -> 211 up -> 212 up on an empty scratch database
# (the scratch database stays in the test container; harmless)
cd /d/PROJECT/ALKAROS
DB=rmd788_mig_$(date +%s)
C=alkaros-test-pg
docker exec $C psql -U postgres -c "CREATE DATABASE $DB;" >/dev/null
run() { MSYS_NO_PATHCONV=1 docker exec -i $C psql -U postgres -d $DB -v ON_ERROR_STOP=1 -1 -q < "$1" >/dev/null || { echo "FAILED $1"; exit 1; }; }
for id in $(python -c "import json;print(' '.join(m['id'] for m in json.load(open('database/MigrationComposition/order.json'))['migrations']))"); do
  f=$(ls database/migrations/*/*/$id-*.up.sql | head -1); run "$f"
done
echo "forward all ok"
q() { docker exec $C psql -U postgres -d $DB -tAc "$1"; }
state() {
  echo "$1: requests=[$(q "select to_regclass('cash.cash_movement_requests')")] reasonCheck=[$(q "select conname from pg_constraint where conname='ck_cash_transactions_movement_reason'")] settingsAuditTrigger=[$(q "select tgname from pg_trigger where tgname='trg_setting_history_audit'")]"
}
state "after up"
M=database/migrations/V1/V1-RMD-788
run $M/212-settings-change-audit.down.sql
run $M/211-cash-movement-requests.down.sql
state "after down"
run $M/211-cash-movement-requests.up.sql
run $M/212-settings-change-audit.up.sql
state "after re-up"
run $M/211-cash-movement-requests.up.sql
run $M/212-settings-change-audit.up.sql
state "after second up (idempotent)"
