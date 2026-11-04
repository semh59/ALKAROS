#!/bin/sh
# Second mutation batch (session lock, self-approval markers, method lookup, allowance spend) and the remaining test projects.
cd /d/PROJECT/ALKAROS
B=tests/Host/Experience/Billing
M=sh\ evidence/V1-RMD-793/mutate.sh
sh evidence/V1-RMD-793/mutate.sh m18-no-share-lock src/Modules/Cash/TransactionLedger/PostgresCashTransactionLedgerRepository.cs '@id FOR SHARE;"' '@id;"' $B "FullyQualifiedName~RefundTerminalSessionHttpTests"
sh evidence/V1-RMD-793/mutate.sh m19-grant-always-self src/Host/Experience/Billing/PaymentRefunds.cs 'selfApproved = cashierUserId == actorId,' 'selfApproved = true,' $B "FullyQualifiedName~RefundSelfApprovalHttpTests"
sh evidence/V1-RMD-793/mutate.sh m20-reject-audit-flag src/Host/Experience/Billing/PaymentRefunds.cs 'requestId, selfApproved = selfDecided })' 'requestId, selfApproved = false })' $B "FullyQualifiedName~RefundSelfApprovalHttpTests"
sh evidence/V1-RMD-793/mutate.sh m21-complete-audit-flag src/Host/Experience/Billing/PaymentRefunds.cs 'viaAllowance = request.ApprovedFromAllowance, selfApproved = request.SelfApproved,' 'viaAllowance = request.ApprovedFromAllowance, selfApproved = false,' $B "FullyQualifiedName~RefundSelfApprovalHttpTests"
sh evidence/V1-RMD-793/mutate.sh m22-any-attempt-is-card src/Host/Experience/Billing/PaymentMethodLookup.cs "s.outcome = 'Approved'" "s.outcome IS NOT NULL" $B "FullyQualifiedName~RefundMethodHttpTests"
sh evidence/V1-RMD-793/mutate.sh m23-spend-ignores-status src/Modules/Cash/SessionLifecycle/PostgresRefundAllowanceRepository.cs "@id AND status = 'Open' AND refund_allowance_used" "@id AND refund_allowance_used" $B "FullyQualifiedName~RefundTerminalSessionHttpTests"
sh evidence/V1-RMD-793/793-run.sh r13-recon tests/Host/Experience/Reconciliation
sh evidence/V1-RMD-793/793-run.sh r13-manifest tests/Host/MigrationComposition "FullyQualifiedName~ManifestTests"
echo done > evidence/V1-RMD-793/chain6.done
