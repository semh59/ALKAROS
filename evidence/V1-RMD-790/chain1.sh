#!/bin/sh
cd /d/PROJECT/ALKAROS
sh evidence/V1-RMD-790/790-run.sh h-httpA tests/Host/Experience/CashSession "FullyQualifiedName~CashSessionHttpTests"
sh evidence/V1-RMD-790/790-run.sh h-httpB tests/Host/Experience/CashSession "FullyQualifiedName~CashMovementApprovalHttpTests"
sh evidence/V1-RMD-790/790-run.sh h-httpC tests/Host/Experience/CashSession "FullyQualifiedName~OpenBillsAndUncertain"
echo done > evidence/V1-RMD-790/chain1.done
