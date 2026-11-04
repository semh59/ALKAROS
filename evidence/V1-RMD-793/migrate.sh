#!/bin/sh
# all migrations up to 214 -> legacy rows -> 215 up (backfill) -> new-kind rows -> 215 down -> 215 up -> 215 up again (idempotent)
cd /d/PROJECT/ALKAROS
DB=rmd793_mig_$(date +%s)
C=alkaros-test-pg
docker exec $C psql -U postgres -c "CREATE DATABASE $DB;" >/dev/null
run() { MSYS_NO_PATHCONV=1 docker exec -i $C psql -U postgres -d $DB -v ON_ERROR_STOP=1 -1 -q < "$1" >/dev/null 2>&1 || { echo "FAILED $1"; exit 1; }; }
sql() { docker exec $C psql -U postgres -d $DB -v ON_ERROR_STOP=1 -tAc "$1"; }
for id in $(python -c "import json;print(' '.join(m['id'] for m in json.load(open('database/MigrationComposition/order.json'))['migrations'] if int(m['id'])<=214))"); do
  f=$(ls database/migrations/*/*/$id-*.up.sql | head -1); run "$f"
done
echo "forward to 214 ok"

U1=11111111-1111-1111-1111-111111111111; U2=22222222-2222-2222-2222-222222222222; TT=33333333-3333-3333-3333-333333333333
B=44444444-4444-4444-4444-444444444444
P1=aaaaaaaa-0000-0000-0000-000000000001; P2=aaaaaaaa-0000-0000-0000-000000000002; P3=aaaaaaaa-0000-0000-0000-000000000003
sql "INSERT INTO identity.users (user_id, username, password_hash, display_name, active) VALUES ('$U1','u793a','x','A',true),('$U2','u793b','x','B',true);" >/dev/null
sql "INSERT INTO billing.bills (bill_id, bill_number, status, payable_amount, currency_code, opened_at, created_at, updated_at) VALUES ('$B','B-793','Open',300,'TRY',now(),now(),now());" >/dev/null
for P in $P1 $P2 $P3; do
  sql "INSERT INTO payments.payments (payment_id, bill_id, status, currency_code, requested_amount, initiated_at, created_at, updated_at) VALUES ('$P','$B','Initiated','TRY',100,now(),now(),now());" >/dev/null
done
sql "INSERT INTO cash.cash_sessions (cash_session_id, cashier_user_id, terminal_id, status, opening_balance, opened_at, created_at, updated_at) VALUES (gen_random_uuid(),'$U1','$TT','Open',0,now() - interval '1 hour',now(),now());" >/dev/null
sql "INSERT INTO payments.manual_card_confirmations (confirmation_id, payment_id, bill_id, slip_number, amount, status, requested_by, requested_at, decided_by, decided_at) VALUES (gen_random_uuid(),'$P2','$B','FIS-793',100,'Approved','$U1',now(),'$U2',now());" >/dev/null
ins() { sql "INSERT INTO payments.refund_requests (refund_request_id, bill_id, payment_id, amount, method, reason, status, requested_by, requested_at) VALUES (gen_random_uuid(),'$B','$1',100,'$2','legacy','Requested','$U1',now());" >/dev/null; }
ins $P1 Cash; ins $P2 Manual; ins $P3 Manual

M=database/migrations/V1/V1-RMD-793
state() { echo "$1: $(sql "SELECT string_agg(payment_id::text || '=' || method || '/' || COALESCE(terminal_id::text,'null'), ' ' ORDER BY payment_id) FROM payments.refund_requests" 2>&1)"; }
sql "SELECT 1" >/dev/null
run $M/215-refund-method-terminal-self-approval.up.sql
echo "after up (P1 cash with terminal $TT, P2 Card, P3 Eft expected)"
sql "SELECT payment_id::text || ' ' || method || ' ' || COALESCE(terminal_id::text,'null') FROM payments.refund_requests ORDER BY payment_id"
# new kinds of rows
sql "UPDATE payments.refund_requests SET status='Rejected', decided_by='$U1', decided_at=now(), approval_mode='Self', decision_note='x' WHERE payment_id='$P3';" >/dev/null && echo "self row accepted"
sql "INSERT INTO payments.manual_card_confirmations (confirmation_id, payment_id, bill_id, slip_number, amount, status, requested_by, requested_at, decided_by, decided_at, self_approved) VALUES (gen_random_uuid(),'$P3','$B','FIS-793-S',100,'Approved','$U1',now(),'$U1',now(),true);" >/dev/null && echo "self approved claim accepted"
if MSYS_NO_PATHCONV=1 docker exec -i $C psql -U postgres -d $DB -v ON_ERROR_STOP=1 -1 -q < $M/215-refund-method-terminal-self-approval.down.sql >/dev/null 2>&1; then echo "UNEXPECTED: down accepted self approved rows"; exit 1; else echo "down refused while self approved rows exist (expected)"; fi
sql "DELETE FROM payments.manual_card_confirmations WHERE self_approved;" >/dev/null
sql "DELETE FROM payments.refund_requests WHERE approval_mode = 'Self';" >/dev/null
sql "INSERT INTO payments.refund_requests (refund_request_id, bill_id, payment_id, amount, method, reason, status, requested_by, requested_at) VALUES (gen_random_uuid(),'$B','$P3',100,'Eft','after','Requested','$U1',now());" >/dev/null
run $M/215-refund-method-terminal-self-approval.down.sql
echo "after down (methods back to Cash/Manual/Manual, terminal column gone)"
sql "SELECT payment_id::text || ' ' || method || ' ' || COALESCE(approval_mode,'-') FROM payments.refund_requests ORDER BY payment_id"
sql "SELECT count(*) FROM information_schema.columns WHERE table_name='manual_card_confirmations' AND column_name='self_approved'"
sql "SELECT count(*) FROM information_schema.columns WHERE table_schema='payments' AND table_name='refund_requests' AND column_name='terminal_id'"
run $M/215-refund-method-terminal-self-approval.up.sql
echo "after re-up"
sql "SELECT payment_id::text || ' ' || method FROM payments.refund_requests ORDER BY payment_id"
run $M/215-refund-method-terminal-self-approval.up.sql
echo "after second up (idempotent) ok"
