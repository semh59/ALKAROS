#!/bin/sh
# all migrations up to 215 -> a fractional order line -> 216 up (backfill) -> 216 down -> 216 up -> 216 up again (idempotent)
cd /d/PROJECT/ALKAROS
DB=rmd800_mig_$(date +%s)
C=alkaros-test-pg
docker exec $C psql -U postgres -c "CREATE DATABASE $DB;" >/dev/null
run() { MSYS_NO_PATHCONV=1 docker exec -i $C psql -U postgres -d $DB -v ON_ERROR_STOP=1 -1 -q < "$1" >/dev/null 2>&1 || { echo "FAILED $1"; exit 1; }; }
sql() { docker exec $C psql -U postgres -d $DB -v ON_ERROR_STOP=1 -tAc "$1"; }
for id in $(python -c "import json;print(' '.join(m['id'] for m in json.load(open('database/MigrationComposition/order.json'))['migrations'] if int(m['id'])<=215))"); do
  f=$(ls database/migrations/*/*/$id-*.up.sql | head -1); run "$f"
done
echo "forward to 215 ok"
P1=aaaaaaaa-0000-0000-0000-000000000001; P2=aaaaaaaa-0000-0000-0000-000000000002; O=bbbbbbbb-0000-0000-0000-000000000001
sql "INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode) VALUES ('$P1','S1','Half sold',1,1),('$P2','S2','Whole sold',1,1);" >/dev/null
sql "INSERT INTO orders.orders (order_id, source, status, confirmation_status, order_number, created_at, updated_at) VALUES ('$O','Cashier','Submitted','NotRequired','O-800',now(),now());" >/dev/null
sql "INSERT INTO orders.order_items (order_item_id, order_id, product_id, product_name_snapshot, quantity, unit_price, tax_rate, tax_amount, net_amount, gross_amount, status, kitchen_state, portion_reservation_status, created_at, updated_at) VALUES (gen_random_uuid(),'$O','$P1','Half sold',1.5,10,10,1.36,13.64,15,'Active','Sent','NotApplicable',now(),now()),(gen_random_uuid(),'$O','$P2','Whole sold',2,10,10,1.82,18.18,20,'Active','Sent','NotApplicable',now(),now());" >/dev/null
M=database/migrations/V1/V1-RMD-800
flags() { echo "$1: $(sql "SELECT string_agg(sku || '=' || allows_half_portion::text, ' ' ORDER BY sku) FROM catalog.products" 2>&1)"; }
run $M/216-product-half-portion.up.sql; flags "after up (history backfill)"
run $M/216-product-half-portion.down.sql; echo "after down: column exists = $(sql "SELECT count(*) FROM information_schema.columns WHERE table_schema='catalog' AND table_name='products' AND column_name='allows_half_portion'")"
run $M/216-product-half-portion.up.sql; flags "after second up"
run $M/216-product-half-portion.up.sql; flags "after repeated up (idempotent)"
docker exec $C psql -U postgres -c "DROP DATABASE $DB;" >/dev/null
