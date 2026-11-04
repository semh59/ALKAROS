#!/bin/sh
cd /d/PROJECT/ALKAROS
R=evidence/V1-RMD-794/794-run.sh
sh $R f-tender tests/Host/Experience/PaymentTender "FullyQualifiedName~TenderRejection|FullyQualifiedName~ATenderOverTheRemaining|FullyQualifiedName~ApprovingAClaimThat|FullyQualifiedName~AFailingAuditWrite|FullyQualifiedName~ConcurrentEftTenders"
sh $R f-cash tests/Host/Experience/CashSession "FullyQualifiedName~CashTenderClaimingMoreThan"
sh $R f-account tests/Host/Experience/CustomerAccounts "FullyQualifiedName~AChargeAboveWhatIsLeft"
echo done > evidence/V1-RMD-794/fix.done
