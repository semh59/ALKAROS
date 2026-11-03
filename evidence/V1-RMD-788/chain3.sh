#!/bin/sh
cd /d/PROJECT/ALKAROS
M=evidence/V1-RMD-788/mutate.sh
H=tests/Host/Experience/CashSession
sh $M m13-no-cross-session-check src/Host/DualScreen/CashMovementStore.cs 'request is null || request.CashSessionId != cashSessionId' 'request is null' $H "FullyQualifiedName~ARequestOfAnotherSession"
sh $M m15-always-self-approved src/Host/DualScreen/CashMovementStore.cs 'var selfApproved = request.RequestedBy == deciderId;' 'var selfApproved = true;' $H "FullyQualifiedName~AnotherManagersApproval"
sh $M m14-no-lock src/Modules/Cash/TransactionLedger/PostgresCashTransactionLedgerRepository.cs 'WHERE cash_session_id = @id FOR UPDATE;' 'WHERE cash_session_id = @id;' $H "FullyQualifiedName~Together|FullyQualifiedName~ParallelDecisions"
sh $M m16-audit-trigger-no-when database/migrations/V1/V1-RMD-788/212-settings-change-audit.up.sql 'FOR EACH ROW WHEN (NEW.old_value IS NOT NULL)' 'FOR EACH ROW' tests/Host/Experience/Settings "FullyQualifiedName~Audit"
sh $M m17-reason-check-validated database/migrations/V1/V1-RMD-788/211-cash-movement-requests.up.sql ') NOT VALID;' ');' $H "FullyQualifiedName~TheDatabaseRefusesAReasonless"
echo done > evidence/V1-RMD-788/chain3.done
