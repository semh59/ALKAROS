#!/bin/sh
cd /d/PROJECT/ALKAROS
M=evidence/V1-RMD-794/mutate.sh
H=tests/Modules/Cash/TenderHandler
T=tests/Host/Experience/PaymentTender
C=tests/Host/Experience/CashSession
P=src/Host/DualScreen/DualScreenApplication.Payments.cs
AUD="FullyQualifiedName~ATenderOverTheRemainingAmountIsRefused|FullyQualifiedName~AFailingAuditWrite"
sh $M m1-replay-ignores-tendered src/Modules/Cash/TenderHandler/CashTenderHandler.cs '            || (payment.ApprovedAmount ?? 0m) + payment.ChangeAmount != request.TenderedAmount)' '            )' $H "FullyQualifiedName~ACashKeyReusedWithAnotherAmountHanded"
sh $M m2-eft-wrong-method $P 'billId, method, ex.Amount, ex.RemainingPayable,' 'billId, TenderMethod.BankCard, ex.Amount, ex.RemainingPayable,' $T "$AUD"
sh $M m3-card-wrong-amount $P 'billId, method, ex.RequestedAmount, ex.RemainingPayable,' 'billId, method, ex.RemainingPayable, ex.RemainingPayable,' $T "$AUD"
sh $M m4-audit-failure-leaks $P 'RejectedTenderAuditFailedLog(loggerFactory.CreateLogger("ALKAROS.Host.DualScreen.TenderRejection"), billId, exception);' 'throw;' $T "$AUD"
sh $M m5-wrong-event-name $P 'eventName: "payment.tender-rejected"' 'eventName: "payment.tender-refused"' $T "$AUD"
sh $M m6-client-tolerance-max src/Clients/PosTerminal/src/features/management-settings-history/models.ts 'kind: "number", min: 0, max: 500,' 'kind: "number", min: 0, max: 501,' $C "FullyQualifiedName~ClientLimitLockTests"
sh $M m7-server-note-limit src/Modules/Cash/Contracts/CashSessionPolicy.cs 'MaxExplanationLength = 500;' 'MaxExplanationLength = 400;' $C "FullyQualifiedName~ClientLimitLockTests"
sh $M m8-english-message $P 'Tanınmayan ödeme yöntemi.' 'Unknown payment method.' $T "FullyQualifiedName~NoResponseMessage"
echo done > evidence/V1-RMD-794/mut.done
