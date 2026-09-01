#!/bin/sh
# ALKAROS write-path load-test fixture seeder (V1-RMD-093).
#
# Seeds the live `alkaros` database with:
#   - N free tables            (table_number 'LT-####')
#   - M sellable products      (sku 'LT-P####', active, priced)
#   - ~1,000,000 background orders + ~3,000,000 items, order_number 'LT-%',
#     status Completed, table_id NULL (never touches a real table pointer),
#     created_at spread over 180 days
# and prints a fixture JSON { "productId": "...", "tableIds": [...] } to stdout.
#
# `--clean` removes every 'LT-' tagged row and nothing else.
#
# Environment: ALKAROS_DB_HOST (postgres) / _PORT / _USER (alkaros) / _NAME (alkaros);
#              password from /run/secrets/db_password or $ALKAROS_DB_PASSWORD.
set -eu

PGHOST="${ALKAROS_DB_HOST:-postgres}"
PGPORT="${ALKAROS_DB_PORT:-5432}"
PGUSER="${ALKAROS_DB_USER:-alkaros}"
PGDATABASE="${ALKAROS_DB_NAME:-alkaros}"
TABLES="${LT_TABLES:-600}"
PRODUCTS="${LT_PRODUCTS:-500}"
ORDERS="${LT_ORDERS:-1000000}"

if [ -f /run/secrets/db_password ]; then
  PGPASSWORD="$(tr -d '\r\n' < /run/secrets/db_password)"
elif [ -n "${ALKAROS_DB_PASSWORD:-}" ]; then
  PGPASSWORD="$ALKAROS_DB_PASSWORD"
else
  echo "seed-loadtest: no database password" >&2
  exit 2
fi
export PGPASSWORD
PSQL="psql -h $PGHOST -p $PGPORT -U $PGUSER -d $PGDATABASE -v ON_ERROR_STOP=1 -qtA"

if [ "${1:-}" = "--clean" ]; then
  echo "seed-loadtest: removing all LT- tagged rows" >&2
  $PSQL -c "DELETE FROM orders.orders WHERE order_number LIKE 'LT-%';"
  $PSQL -c "DELETE FROM table_mgmt.tables WHERE table_number LIKE 'LT-%';"
  $PSQL -c "DELETE FROM catalog.products WHERE sku LIKE 'LT-P%';"
  $PSQL -c "VACUUM (ANALYZE) orders.orders;" || true
  echo "seed-loadtest: clean done" >&2
  exit 0
fi

echo "seed-loadtest: $TABLES tables, $PRODUCTS products, $ORDERS background orders" >&2

$PSQL -c "
INSERT INTO catalog.products (product_id, sku, name, product_type, stock_mode, active, display_order, current_price)
SELECT gen_random_uuid(), 'LT-P' || lpad(g::text, 5, '0'), 'Load Test Product ' || g,
       1, 1, true, g, (10 + (g % 40))::numeric(18,2)
FROM generate_series(1, $PRODUCTS) g
ON CONFLICT (sku) DO NOTHING;"

$PSQL -c "
INSERT INTO table_mgmt.tables (table_id, zone_id, table_number, capacity, active, current_status, current_order_id, current_bill_id, row_version)
SELECT gen_random_uuid(), NULL, 'LT-' || lpad(g::text, 4, '0'), 4, true, 'Available', NULL, NULL, 1
FROM generate_series(1, $TABLES) g
WHERE NOT EXISTS (SELECT 1 FROM table_mgmt.tables WHERE table_number = 'LT-' || lpad(g::text, 4, '0'));"

echo "seed-loadtest: inserting $ORDERS orders (this takes ~20-40s)" >&2
$PSQL -c "
INSERT INTO orders.orders
 (order_id, source, table_id, status, confirmation_status, order_number, subtotal, discount_total, tax_total, total, currency_code, submitted_at, created_at, updated_at, row_version)
SELECT gen_random_uuid(), 'Cashier', NULL,
       (ARRAY['Completed','Completed','Completed','Cancelled','Served'])[1 + floor(random()*5)],
       'NotRequired', 'LT-' || g,
       (random()*400)::numeric(18,2), 0, 0, (random()*440)::numeric(18,2), 'TRY',
       now() - make_interval(days => (random()*180)::int, secs => (random()*86400)::int),
       now() - make_interval(days => (random()*180)::int, secs => (random()*86400)::int), now(), 1
FROM generate_series(1, $ORDERS) g;"

echo "seed-loadtest: inserting order items" >&2
$PSQL -c "
WITH pids AS (SELECT array_agg(product_id) AS a FROM catalog.products WHERE sku LIKE 'LT-P%')
INSERT INTO orders.order_items
 (order_item_id, order_id, product_id, product_name_snapshot, quantity, unit_price, tax_rate, tax_amount, net_amount, gross_amount, status, kitchen_state, portion_reservation_status, created_at, updated_at, row_version)
SELECT gen_random_uuid(), o.order_id,
       (SELECT a FROM pids)[1 + floor(random() * array_length((SELECT a FROM pids), 1))::int],
       'Item', 1, 40, 10, 4, 40, 44,
       'Active', 'Served', 'NotApplicable', o.created_at, o.created_at, 1
FROM orders.orders o, generate_series(1, 3)
WHERE o.order_number LIKE 'LT-%';"

$PSQL -c "VACUUM (ANALYZE) orders.orders;"
$PSQL -c "VACUUM (ANALYZE) orders.order_items;"
$PSQL -c "ANALYZE table_mgmt.tables; ANALYZE catalog.products;"

PRODUCT_ID="$($PSQL -c "SELECT product_id FROM catalog.products WHERE sku LIKE 'LT-P%' ORDER BY sku LIMIT 1;")"
TABLE_IDS="$($PSQL -c "SELECT string_agg('\"' || table_id || '\"', ',') FROM table_mgmt.tables WHERE table_number LIKE 'LT-%';")"

printf '{ "productId": "%s", "tableIds": [%s] }\n' "$PRODUCT_ID" "$TABLE_IDS"
echo "seed-loadtest: done" >&2
