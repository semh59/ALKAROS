#!/bin/sh
cd /d/PROJECT/ALKAROS
F=src/Modules/Cash/Contracts/CashSessionPolicy.cs; P=tests/Modules/Cash/Contracts
M=evidence/V1-RMD-790/mutate.sh
sh $M m1-tolerance-gte $F 'Math.Abs(difference) > input.VarianceTolerance' 'Math.Abs(difference) >= input.VarianceTolerance' $P
sh $M m2-mismatch $F 'recorded != input.ActualCash' 'recorded < 0' $P
sh $M m3-explanation $F '(difference != 0 && explanation is null) || ' '' $P
sh $M m4-owner $F 'session.CashierUserId != actorUserId && !actorMayActForOwner' 'session.CashierUserId != actorUserId && actorMayActForOwner' $P
sh $M m5-selfreconcile $F 'selfReconcile && !selfReconcileAllowed' 'selfReconcile && selfReconcileAllowed' $P
sh $M m6-closer $F ' || reconcilerUserId == closedBy;' ';' $P
sh $M m7-explen $F 'Length: > MaxExplanationLength' 'Length: > 5000' $P
sh $M m8-nocount $F 'input.LatestCountedAmount is not { } recorded' 'input.LatestCountedAmount is not { } recorded && false' $P
echo done > evidence/V1-RMD-790/chain4.done
