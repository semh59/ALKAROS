#!/bin/sh
cd /d/PROJECT/ALKAROS
sh evidence/V1-RMD-790/790-run.sh i-httpA tests/Host/Experience/CashSession "FullyQualifiedName~CashSessionHttpTests"
sh evidence/V1-RMD-790/790-run.sh i-httpB tests/Host/Experience/CashSession "FullyQualifiedName~CashMovementApprovalHttpTests"
sh evidence/V1-RMD-790/790-run.sh i-safety tests/Host/Experience/CashSession "FullyQualifiedName~CashCloseSafetyHttpTests"
echo done > evidence/V1-RMD-790/chain2.done
