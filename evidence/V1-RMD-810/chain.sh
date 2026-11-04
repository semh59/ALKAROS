#!/bin/sh
# Runs the final green passes one after another; each writes its own exit.txt.
R=D:/PROJECT/ALKAROS/evidence/V1-RMD-810
sh $R/run.sh final-tender tests/Host/Experience/PaymentTender
sh $R/run.sh final-billing tests/Host/Experience/Billing
sh $R/run.sh final-arch-api tests/Architecture/ApiConventions
sh $R/run.sh final-arch-boundaries tests/Architecture/ModuleBoundaries
sh $R/run.sh final-host-dual tests/Host/MigrationComposition "FullyQualifiedName~Host.Tests.DualScreen"
echo done > $R/chain.done
