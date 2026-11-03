#!/bin/sh
cd /d/PROJECT/ALKAROS
R=evidence/V1-RMD-788/788-run.sh
sh $R f-host tests/Host/Experience/CashSession
sh $R f-settings tests/Host/Experience/Settings
sh $R f-errors tests/Host/Errors
sh $R f-ledger tests/Modules/Cash/TransactionLedger
sh $R f-receipts tests/Modules/CustomerAccounts/CashReceipts
sh $R f-manifest tests/Host/MigrationComposition "FullyQualifiedName~ManifestTests"
sh $R f-apiconv tests/Architecture/ApiConventions
sh $R f-modbound tests/Architecture/ModuleBoundaries
echo done > evidence/V1-RMD-788/chain.done
