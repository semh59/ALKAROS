#!/bin/sh
cd /d/PROJECT/ALKAROS
M=evidence/V1-RMD-794/mutate.sh
sh $M m9-cash-wrong-method src/Host/DualScreen/DualScreenApplication.CashSession.cs 'terminalId, request.BillId, "Cash",' 'terminalId, request.BillId, "Card",' tests/Host/Experience/CashSession "FullyQualifiedName~CashTenderClaimingMoreThan"
sh $M m10-account-wrong-method src/Host/Experience/CustomerAccounts/DualScreenApplication.CustomerAccounts.cs 'billId, "Account",' 'billId, "Cash",' tests/Host/Experience/CustomerAccounts "FullyQualifiedName~AChargeAboveWhatIsLeft"
sh $M m11-claim-not-named src/Host/DualScreen/DualScreenApplication.Payments.cs 'exception.RemainingPayable, null, confirmationId);' 'exception.RemainingPayable, null, null);' tests/Host/Experience/PaymentTender "FullyQualifiedName~ApprovingAClaimThat"
echo done > evidence/V1-RMD-794/mut2.done
