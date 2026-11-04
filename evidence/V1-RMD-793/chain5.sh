#!/bin/sh
cd /d/PROJECT/ALKAROS
sh evidence/V1-RMD-793/793-run.sh e-billing tests/Host/Experience/Billing
sh evidence/V1-RMD-793/793-run.sh e-recon tests/Host/Experience/Reconciliation "FullyQualifiedName~ManualCardConfirmationConstraintTests"
sh evidence/V1-RMD-793/793-run.sh e-tender tests/Host/Experience/PaymentTender "FullyQualifiedName~ACardChargeConfirmedByASecondPerson|FullyQualifiedName~TheClaimantApprovesTheirOwn|FullyQualifiedName~BillReceiptHttpTests"
sh evidence/V1-RMD-793/793-run.sh e-margin tests/Modules/Reporting/ProductMargin
echo done > evidence/V1-RMD-793/chain5.done
