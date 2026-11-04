#!/bin/sh
cd /d/PROJECT/ALKAROS
M=evidence/V1-RMD-794/mutate.sh
C=tests/Host/Experience/CashSession
T=tests/Host/Experience/PaymentTender
#sh $M m12-minutes-range src/Clients/PosTerminal/src/features/management-settings-history/models.ts 'kind: "minutes", min: 1, max: 1440,' 'kind: "minutes", min: 1, max: 1441,' $C "FullyQualifiedName~ClientLimitLockTests"
#sh $M m13-lower-bound src/Clients/PosTerminal/src/features/management-settings-history/models.ts 'kind: "number", min: 1, max: 99,' 'kind: "number", min: 0, max: 99,' $C "FullyQualifiedName~ClientLimitLockTests"
#sh $M m14-refund-text-limit src/Clients/PosTerminal/src/features/billing/RefundPanel.tsx 'maxLength={500}' 'maxLength={400}' $C "FullyQualifiedName~ClientLimitLockTests"
#sh $M m15-refund-slip-limit src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js 'type="text" maxlength="50" data-refund-slip' 'type="text" maxlength="60" data-refund-slip' $C "FullyQualifiedName~ClientLimitLockTests"
sh $M m16-claim-slip-limit src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js 'id="claim-slip" maxlength="32"' 'id="claim-slip" maxlength="40"' $T "FullyQualifiedName~TheCardClaimSlipLength"
sh $M m17-audit-log-removed src/Host/DualScreen/DualScreenApplication.Payments.cs 'RejectedTenderAuditFailedLog(loggerFactory.CreateLogger("ALKAROS.Host.DualScreen.TenderRejection"), billId, exception);' '_ = exception;' $T "FullyQualifiedName~AnAuditStoreThatFails"
sh $M m18-always-record src/Host/DualScreen/DualScreenApplication.Payments.cs 'idempotencyKey = idempotencyKey is { Length: > 100 } ? idempotencyKey[..100] : idempotencyKey,' 'idempotencyKey,' $T "FullyQualifiedName~AnIdempotencyKeyLonger"
sh $M m19-refund-english src/Host/Experience/Billing/PaymentRefunds.cs '"İade tutarı sıfırdan büyük olmalı."' '"The refund amount must be above zero."' $T "FullyQualifiedName~NoResponseMessage"
echo done > evidence/V1-RMD-794/mut3.done
