#!/bin/sh
cd /d/PROJECT/ALKAROS
M=evidence/V1-RMD-794/mutate.sh
C=tests/Host/Experience/CashSession
T=tests/Host/Experience/PaymentTender
A=tests/Host/Experience/CustomerAccounts
P=src/Host/DualScreen/DualScreenApplication.Payments.cs
sh $M m21-terminal-lost $P 'remainingPayable, terminalId, confirmationId,' 'remainingPayable, terminalId = Guid.Empty, confirmationId,' $T "FullyQualifiedName~ATenderOverTheRemaining"
sh $M m22-no-own-deadline $P 'deadline.Token);' 'CancellationToken.None);' $T "FullyQualifiedName~AnIdempotencyKeyLonger"
sh $M m23-cash-actor-lost src/Host/DualScreen/DualScreenApplication.CashSession.cs 'context, principal.UserId, terminalId, request.BillId, "Cash",' 'context, Guid.Empty, terminalId, request.BillId, "Cash",' $C "FullyQualifiedName~CashTenderClaimingMoreThan"
sh $M m24-account-amount-lost src/Host/Experience/CustomerAccounts/DualScreenApplication.CustomerAccounts.cs 'exception.RequestedAmount, exception.RemainingPayable, request.IdempotencyKey);' 'exception.RemainingPayable, exception.RemainingPayable, request.IdempotencyKey);' $A "FullyQualifiedName~AChargeAboveWhatIsLeft"
sh $M m25-eft-actor-lost $P 'context, principal.UserId, terminalId, billId, method.ToString(), ex.Amount,' 'context, Guid.Empty, terminalId, billId, method.ToString(), ex.Amount,' $T "FullyQualifiedName~ATenderOverTheRemaining"
sh $M m26-refund-screen-limit src/Clients/PosTerminal/src/features/billing/RefundPanel.tsx 'maxLength={50}' 'maxLength={60}' $C "FullyQualifiedName~ClientLimitLockTests"
echo done > evidence/V1-RMD-794/mut4.done
