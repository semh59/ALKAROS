#!/bin/sh
cd /d/PROJECT/ALKAROS
M=evidence/V1-RMD-793/mutate.sh
B=tests/Host/Experience/Billing
L=src/Host/Experience/Billing/PaymentMethodLookup.cs
R=src/Host/Experience/Billing/PaymentRefunds.cs
Q=src/Modules/Payments/PaymentAggregate/PostgresRefundRequestRepository.cs
sh $M m1-card-evidence-lost $L "m.status = 'Approved'" "m.status = 'Pending'" $B "FullyQualifiedName~ACardRefundStillNeedsTheSlip"
sh $M m2-transfer-needs-slip $R 'var slip = request.Method == PaymentMethodLookup.Card' 'var slip = request.Method != PaymentMethodLookup.Cash' $B "FullyQualifiedName~ATransferRefundNeedsNoSlip"
sh $M m3-newest-till-again $R "WHERE terminal_id = @terminal AND cashier_user_id = @user AND status = 'Open';" "WHERE cashier_user_id = @user AND status = 'Open' ORDER BY opened_at DESC LIMIT 1;" $B "FullyQualifiedName~ACashRefundIsPaidOutOfTheTill|FullyQualifiedName~ARefundAskedAtATerminalWhere"
sh $M m4-self-always-allowed $R 'if (requestedBy == deciderId && !selfApprovalAllowed)' 'if (requestedBy == deciderId && selfApprovalAllowed)' $B "FullyQualifiedName~TheRequesterDecidesTheirOwnRefund|FullyQualifiedName~ARefundIsNotDecided"
sh $M m5-self-not-marked $R 'var mode = request.RequestedBy == deciderId ? "Self" : "Manager";' 'var mode = "Manager";' $B "FullyQualifiedName~TheRequesterDecidesTheirOwnRefund"
sh $M m6-filter-inverted $Q "(approval_mode = 'Self') = @self" "(approval_mode = 'Self') <> @self" $B "FullyQualifiedName~TheRequesterDecidesTheirOwnRefund|FullyQualifiedName~ADecisionByAnotherPerson"
sh $M m7-allowance-any-till src/Modules/Cash/SessionLifecycle/PostgresRefundAllowanceRepository.cs 'AND terminal_id = @terminal ' '' $B "FullyQualifiedName~TheRefundAllowanceBelongsToTheTill"
sh $M m9-audit-flag-false $R 'amount = request.Amount, selfApproved = request.SelfApproved,' 'amount = request.Amount, selfApproved = false,' $B "FullyQualifiedName~TheRequesterDecidesTheirOwnRefund"
sh $M m10-allowance-read-newest $R "WHERE cashier_user_id = @user AND terminal_id = @terminal AND status = 'Open';" "WHERE cashier_user_id = @user AND status = 'Open' ORDER BY opened_at DESC LIMIT 1;" $B "FullyQualifiedName~TheRefundAllowanceBelongsToTheTill|FullyQualifiedName~TheAllowanceReadsEmptyAtATerminal"
sh $M m12-legacy-no-terminal $R 'if (sessionId is null)' 'if (sessionId is null && false)' $B "FullyQualifiedName~ARequestFromBeforeTheTerminal"
sh $M m14-self-reject-unmarked $R 'selfDecided ? "Self" : null' 'null' $B "FullyQualifiedName~TheRequesterRejectsTheirOwnRefund"
sh $M m16-self-grant-open $R 'EnsureMayDecide(cashierUserId, actorId, selfApprovalAllowed);' '' $B "FullyQualifiedName~OnlyAManagerGrantsTheAllowance"
sh $M m8-card-self-open src/Modules/Payments/ManualResolution/ManualCardConfirmationService.cs 'if (selfApproved && !selfApprovalAllowed)' 'if (selfApproved && selfApprovalAllowed)' tests/Host/Experience/PaymentTender "FullyQualifiedName~TheClaimantApprovesTheirOwn|FullyQualifiedName~ACardChargeClaimedByOneManager"
sh $M m17-receipt-eft-label src/Host/Experience/Billing/PaymentMethodLookup.cs "ELSE '{Eft}' END" "ELSE 'Manual' END" tests/Host/Experience/PaymentTender "FullyQualifiedName~BillReceiptHttpTests"
echo done > evidence/V1-RMD-793/chain3.done
