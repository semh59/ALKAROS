#!/bin/sh
cd /d/PROJECT/ALKAROS
R=sh; X=evidence/V1-RMD-790/790-run.sh
$R $X l-contracts tests/Modules/Cash/Contracts
$R $X l-lifecycle tests/Modules/Cash/SessionLifecycle
$R $X l-ledger tests/Modules/Cash/TransactionLedger
$R $X l-settings tests/Host/Experience/Settings "FullyQualifiedName~SettingsManagementHttpTests"
$R $X l-safety tests/Host/Experience/CashSession "FullyQualifiedName~CashCloseSafetyHttpTests"
$R $X l-httpA tests/Host/Experience/CashSession "FullyQualifiedName~CashSessionHttpTests"
$R $X l-httpB tests/Host/Experience/CashSession "FullyQualifiedName~CashMovementApprovalHttpTests"
$R $X l-refund tests/Host/Experience/Billing "FullyQualifiedName~PaymentRefundHttpTests"
echo done > evidence/V1-RMD-790/chain7.done
