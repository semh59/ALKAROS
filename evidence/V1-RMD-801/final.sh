#!/bin/sh
cd /d/PROJECT/ALKAROS
R=evidence/V1-RMD-801/801-run.sh
L=$1
sh $R $L-billing tests/Host/Experience/Billing
sh $R $L-domain tests/Modules/Orders/OrderAggregate
sh $R $L-arch tests/Architecture/ModuleBoundaries
sh $R $L-apiconv tests/Architecture/ApiConventions
sh $R $L-tables tests/Host/Experience/Tables
sh $R $L-billing-modules tests/Modules/Billing/PaymentClosure
sh $R $L-voidsent tests/Host/Experience/Orders/VoidSent
sh $R $L-void tests/Host/Experience/Orders/Void
sh $R $L-comp tests/Host/Experience/Orders/Comp
sh $R $L-confirmation tests/Host/Experience/Orders/Confirmation
sh $R $L-tabledraft tests/Host/Experience/Orders/TableDraft
sh $R $L-secadmin tests/Host/Experience/SecurityAdministration
sh $R $L-paymenttender tests/Host/Experience/PaymentTender
echo done > evidence/V1-RMD-801/$L-batch.done
