#!/bin/sh
cd /d/PROJECT/ALKAROS
R=evidence/V1-RMD-788/788-run.sh
sh $R l-settings tests/Host/Experience/Settings
sh $R l-errors tests/Host/Errors
sh $R l-ledger tests/Modules/Cash/TransactionLedger
sh $R l-receipts tests/Modules/CustomerAccounts/CashReceipts
sh $R l-manifest tests/Host/MigrationComposition "FullyQualifiedName~ManifestTests"
sh $R l-apiconv tests/Architecture/ApiConventions
sh $R l-modbound tests/Architecture/ModuleBoundaries
echo done > evidence/V1-RMD-788/chain2.done
