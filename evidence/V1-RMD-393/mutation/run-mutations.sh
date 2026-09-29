#!/bin/sh
# V1-RMD-393 mutation check. Runs in a disposable clone (never the working repository): each mutation breaks
# one existing money-flow guard, rebuilds, runs the test projects that own that guard (its module tests plus the
# host HTTP tests of the same flow) and records whether ANY of them failed ("killed") or none did ("survived" =
# the guard is untested there). The clone is reset between runs.
set -u
ROOT=${1:-/mut}
cd "$ROOT" || exit 2
mutate() { # id file perl-substitution description test-projects
  id=$1; file=$2; expr=$3; desc=$4; TESTS=$5
  git checkout -q -- .
  before=$(sha256sum "$file" | cut -d' ' -f1)
  perl -0pi -e "$expr" "$file"
  after=$(sha256sum "$file" | cut -d' ' -f1)
  if [ "$before" = "$after" ]; then echo "$id | NOT-APPLIED | $desc"; return; fi
  git diff --stat -- "$file" | tail -1 > /tmp/$id.diffstat
  if ! dotnet build ALKAROS.slnx -c Release --no-restore -v q > /tmp/$id.build.log 2>&1; then
    echo "$id | COMPILE-ERROR | $desc"; return
  fi
  failed=""
  for t in $TESTS; do
    if ! dotnet test "$t" -c Release --no-build > /tmp/$id.$(echo $t | tr / _).log 2>&1; then failed="$failed $t"; fi
  done
  if [ -n "$failed" ]; then echo "$id | KILLED by:$failed | $desc"; else echo "$id | SURVIVED | $desc"; fi
}

mutate M01 src/Modules/Payments/EftTender/EftTenderHandler.cs \
  's/if \(unsettledPayment is not null\)\s*\n\s*throw new EftUnsettledPaymentExistsException\([^;]*;/\/\* M01 \*\//' \
  "EFT: drop the unresolved-payment lock (V1-RMD-258)" \
  "tests/Modules/Payments/EftTender tests/Host/Experience/PaymentTender"
mutate M02 src/Modules/Cash/TenderHandler/CashTenderHandler.cs \
  's/await LockIdempotencyKeyAsync\(connection, dbTransaction, request.IdempotencyKey, cancellationToken\);/\/\* M02 \*\//' \
  "Cash: drop the per-idempotency-key advisory lock" \
  "tests/Modules/Cash/TenderHandler tests/Host/Experience/CashSession"
mutate M03 src/Modules/Billing/PaymentClosure/BillPaymentClosureCalculator.cs \
  's/\.Where\(a => approvedPaymentIds\.Contains\(a\.PaymentId\)\)\s*\n\s*\.Sum\(a => a\.Amount\)/.Sum(a => a.Amount)/' \
  "Closure: count allocations of non-Approved payments as paid" \
  "tests/Modules/Billing/PaymentClosure tests/Host/Experience/PaymentTender"
mutate M04 src/Modules/Cash/TransactionLedger/PostgresCashTransactionLedgerRepository.cs \
  "s/\n\s*AND type NOT IN \('CountAdjustment', 'ClosingDifference'\)//" \
  "Expected cash: include count adjustments / closing differences" \
  "tests/Modules/Cash/TransactionLedger tests/Host/Experience/CashSession"
mutate M05 src/Modules/Payments/Allocations/Persistence/PaymentAllocationFactory.cs \
  's/if \(amount > remaining\)\s*\n\s*throw new OverAllocationException\(bill\.Id, amount, remaining\);/\/\* M05 \*\//' \
  "Allocation: drop the remaining-payable ceiling (V0-DOM-004)" \
  "tests/Modules/Payments/Allocations/Persistence tests/Modules/Cash/TenderHandler tests/Modules/Payments/EftTender tests/Host/Experience/PaymentTender"
mutate M06 src/Modules/Billing/Adjustments/AdjustmentCalculator.cs \
  's/totalBasePayable - discountGross \+ feeGross \+ tipGross/totalBasePayable - discountGross + feeGross/' \
  "Adjusted payable: ignore tips" \
  "tests/Modules/Billing/Adjustments tests/Modules/Billing/PaymentClosure tests/Host/Experience/Billing tests/Host/Experience/PaymentTender"
mutate M07 src/Modules/Billing/PaymentClosure/BillClosureService.cs \
  's/if \(!projection\.PaymentSatisfied \|\| projection\.Blockers\.Count > 0\)/if (!projection.PaymentSatisfied)/' \
  "Closure: close a bill even with Pending/Unknown payment blockers" \
  "tests/Modules/Billing/PaymentClosure tests/Host/Experience/PaymentTender"
mutate M08 src/Modules/Cash/TenderHandler/CashTenderHandler.cs \
  's/var changeAmount = request\.TenderedAmount - approvedAmount;/var changeAmount = 0m;/' \
  "Cash: never return change" \
  "tests/Modules/Cash/TenderHandler tests/Host/Experience/CashSession"
mutate M09 src/Modules/Payments/CardSettlement/CardSettlementOrchestrator.cs \
  's/if \(unsettled is not null\)\s*\n\s*throw new CardSettlementUnsettledPaymentExistsException\([^;]*;/\/\* M09 \*\//' \
  "Card: allow a new attempt on top of an unresolved one" \
  "tests/Modules/Payments/CardSettlement tests/Host/Experience/PaymentTender"
mutate M10 src/Modules/Payments/ManualResolution/ManualCardConfirmationService.cs \
  's/if \(confirmation\.RequestedBy == approverUserId\)\s*\n\s*throw new ManualResolutionSameActorException\(\);/\/\* M10 \*\//' \
  "Manual card approval: allow the claimant to approve their own claim (four-eyes)" \
  "tests/Host/Experience/PaymentTender"
git checkout -q -- .
