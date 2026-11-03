#!/bin/sh
cd /d/PROJECT/ALKAROS
M=evidence/V1-RMD-790/mutate.sh; P=tests/Host/Experience/CashSession; Fl="FullyQualifiedName~CashCloseSafetyHttpTests"
S=src/Host/DualScreen/CashCloseStore.cs; V=src/Modules/Cash/SessionLifecycle/CashSessionLifecycleService.cs
sh $M s1-ack $S 'unsettled.Any && !acknowledged' 'false' $P "$Fl"
sh $M s2-bills $S "status NOT IN ('Paid', 'Cancelled')" "status NOT IN ('Paid', 'Cancelled', 'Open')" $P "$Fl"
sh $M s3-payments $S "'Pending', 'Unknown', 'ReconciliationRequired'" "'Pending'" $P "$Fl"
sh $M s4-ledger $V 'if (difference != 0)' 'if (false)' $P "$Fl"
sh $M s5-direction $V 'difference > 0 ? CashTransactionDirection.In : CashTransactionDirection.Out' 'difference > 0 ? CashTransactionDirection.Out : CashTransactionDirection.In' $P "$Fl"
sh $M s6-clamp src/Modules/Settings/CashCloseTolerance/CloseVarianceToleranceSetting.cs 'Math.Clamp(value, 0, MaxValue)' 'value' $P "$Fl"
sh $M s7-auditname $S '"cash.session.closed"' '"cash.session.closedx"' $P "$Fl"
sh $M s8-reconaudit $S '"cash.session.reconciled"' '"cash.session.reconciledx"' $P "$Fl"
echo done > evidence/V1-RMD-790/chain5.done
