#!/bin/sh
# V1-RMD-398 mutation check (same method as V1-RMD-393): in a disposable clone, break one existing stock guard,
# rebuild, run the test projects that own it, record KILLED (some test failed) or SURVIVED (guard untested there).
set -u
ROOT=${1:-/mut398}
cd "$ROOT" || exit 2
mutate() { # id file perl-substitution description test-projects
  id=$1; file=$2; expr=$3; desc=$4; TESTS=$5
  git checkout -q -- src
  before=$(sha256sum "$file" | cut -d' ' -f1)
  perl -0pi -e "$expr" "$file"
  after=$(sha256sum "$file" | cut -d' ' -f1)
  if [ "$before" = "$after" ]; then echo "$id | NOT-APPLIED | $desc"; return; fi
  if ! dotnet build ALKAROS.slnx -c Release --no-restore -v q > /tmp/$id.build.log 2>&1; then
    echo "$id | COMPILE-ERROR | $desc"; return
  fi
  failed=""
  for t in $TESTS; do
    if ! dotnet test "$t" -c Release --no-build > /tmp/$id.$(echo $t | tr / _).log 2>&1; then failed="$failed $t"; fi
  done
  if [ -n "$failed" ]; then echo "$id | KILLED by:$failed | $desc"; else echo "$id | SURVIVED | $desc"; fi
}

mutate N01 src/Modules/Inventory/CrossChannelReservation/PostgresReservationAwareConsumptionGuard.cs \
  's/availableQuantity >= quantity;/availableQuantity >= 0m;/' \
  "Consumption guard: ignore the requested quantity (oversell)" \
  "tests/Modules/Inventory/CrossChannelReservation tests/Host/Experience/Orders/VoidSent"
mutate N02 src/Modules/Inventory/BalanceProjection/PostgresStockBalanceRepository.cs \
  's/WHERE inventory\.stock_balances\.on_hand_quantity \+ EXCLUDED\.on_hand_quantity >= 0/WHERE true/' \
  "Balance guard: allow on-hand to go negative" \
  "tests/Modules/Inventory/BalanceProjection tests/Modules/Inventory/PhysicalCounts tests/Modules/Inventory/WasteRecording"
mutate N03 src/Host/Experience/Orders/SentItemVoid/SentItemVoidStore.cs \
  's/item\.KitchenState is KitchenState\.Sent or KitchenState\.Held/item.KitchenState is KitchenState.Sent/' \
  "Void: do not restore stock of a Held course (V1-RMD-318)" \
  "tests/Host/Experience/Orders/VoidSent"
mutate N04 src/Modules/Inventory/WasteRecording/WasteRecordingService.cs \
  's/if \(existingRecord != null\)/if (existingRecord != null \&\& false)/' \
  "Waste: drop idempotent replay" \
  "tests/Modules/Inventory/WasteRecording"
mutate N05 src/Modules/Production/StockEffects/ProductionStockEffectService.cs \
  's/\(1\.0m \+ wasteFactor\)/(1.0m)/' \
  "Production: ignore the ingredient loss percentage" \
  "tests/Modules/Production/StockEffects tests/Host/Experience/Production"
mutate N06 src/Modules/Production/StockEffects/ProductionStockEffectService.cs \
  's/if \(string\.Equals\(status, "Completed", StringComparison\.OrdinalIgnoreCase\)\)/if (status.Length < 0)/' \
  "Production: re-execute a Completed batch (double consumption)" \
  "tests/Modules/Production/StockEffects tests/Host/Experience/Production"
mutate N07 src/Host/Experience/Orders/OrderStockConsumption/OrderStockConsumptionService.cs \
  's/if \(item\.Status == OrderItemState\.Cancelled\)\n                continue;/if (item.Id == Guid.Empty)\n                continue;/' \
  "Sale consumption: consume stock for a line voided before acceptance" \
  "tests/Host/Experience/Orders/Confirmation tests/Host/Experience/Orders/VoidSent"
mutate N08 src/Modules/Recipes/CostSnapshots/IStockCostResolver.cs \
  's/CAST\(gr\.received_at AS date\) <= \$2/CAST(gr.received_at AS date) < \$2/' \
  "Cost resolver: exclude receipts on the basis date (the blind-calibration seed itself)" \
  "tests/Modules/Recipes/CostSnapshots tests/Host/Experience/Recipes"
git checkout -q -- src
