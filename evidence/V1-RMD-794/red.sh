#!/bin/sh
cd /d/PROJECT/ALKAROS
sh evidence/V1-RMD-794/794-run.sh red-handler tests/Modules/Cash/TenderHandler "FullyQualifiedName~ACashKeyReusedWithAnotherAmountHanded"
sh evidence/V1-RMD-794/794-run.sh red-audit tests/Host/Experience/PaymentTender "FullyQualifiedName~ATenderOverTheRemainingAmountIsRefused|FullyQualifiedName~AFailingAuditWrite|FullyQualifiedName~ATenderWithinTheRemaining|FullyQualifiedName~NoResponseMessage"
sh evidence/V1-RMD-794/794-run.sh red-limits tests/Host/Experience/CashSession "FullyQualifiedName~ClientLimitLockTests"
echo done > evidence/V1-RMD-794/red.done
