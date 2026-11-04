#!/bin/sh
cd /d/PROJECT/ALKAROS
sh evidence/V1-RMD-793/793-run.sh d-tender tests/Host/Experience/PaymentTender "FullyQualifiedName~ACardChargeConfirmedByASecondPerson|FullyQualifiedName~TheClaimantApprovesTheirOwn"
sh evidence/V1-RMD-793/793-run.sh d-apiconv tests/Architecture/ApiConventions
sh evidence/V1-RMD-793/793-run.sh d-bounds tests/Architecture/ModuleBoundaries
sh evidence/V1-RMD-793/793-run.sh d-errors tests/Host/Errors
sh evidence/V1-RMD-793/793-run.sh d-margin tests/Modules/Reporting/ProductMargin
echo done > evidence/V1-RMD-793/chain2.done
