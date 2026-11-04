#!/bin/sh
cd /d/PROJECT/ALKAROS
sh evidence/V1-RMD-793/793-run.sh c-tender tests/Host/Experience/PaymentTender
sh evidence/V1-RMD-793/793-run.sh c-recon tests/Host/Experience/Reconciliation "FullyQualifiedName~ManualCardConfirmationConstraintTests"
sh evidence/V1-RMD-793/793-run.sh c-billing tests/Host/Experience/Billing
sh evidence/V1-RMD-793/793-run.sh c-manifest tests/Host/MigrationComposition "FullyQualifiedName~ManifestTests"
echo done > evidence/V1-RMD-793/chain1.done
