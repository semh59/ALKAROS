#!/bin/sh
cd /d/PROJECT/ALKAROS
R=evidence/V1-RMD-794/794-run.sh
sh $R h-limits tests/Host/Experience/CashSession "FullyQualifiedName~ClientLimitLockTests"
sh $R h-tenderNew tests/Host/Experience/PaymentTender "FullyQualifiedName~TenderRejection|FullyQualifiedName~ErrorMessageLanguage|FullyQualifiedName~ATenderOverTheRemaining|FullyQualifiedName~ApprovingAClaim|FullyQualifiedName~AnAuditStore|FullyQualifiedName~AnIdempotencyKeyLonger|FullyQualifiedName~AFailingAudit|FullyQualifiedName~TheCardClaimSlipLength"
echo done > evidence/V1-RMD-794/final.done
