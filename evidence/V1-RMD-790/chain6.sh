#!/bin/sh
cd /d/PROJECT/ALKAROS
sh evidence/V1-RMD-790/790-run.sh k-lifecycle tests/Modules/Cash/SessionLifecycle
sh evidence/V1-RMD-790/790-run.sh k-ledger tests/Modules/Cash/TransactionLedger
sh evidence/V1-RMD-790/790-run.sh k-safety tests/Host/Experience/CashSession "FullyQualifiedName~CashCloseSafetyHttpTests"
echo done > evidence/V1-RMD-790/chain6.done
