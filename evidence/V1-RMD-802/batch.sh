#!/bin/sh
cd /d/PROJECT/ALKAROS
R=evidence/V1-RMD-802/802-run.sh
L=$1
sh $R $L-hub tests/Host/Experience/WaiterNotifications
sh $R $L-closure tests/Modules/Billing/PaymentClosure
sh $R $L-policy tests/Modules/Tables/PaymentTopology
sh $R $L-contract tests/Host/MigrationComposition/DualScreen
sh $R $L-arch tests/Architecture/ModuleBoundaries
sh $R $L-apiconv tests/Architecture/ApiConventions
sh $R $L-secadmin tests/Host/Experience/SecurityAdministration
sh $R $L-tabledraft tests/Host/Experience/Orders/TableDraft
sh $R $L-paymenttender tests/Host/Experience/PaymentTender
sh $R $L-tables tests/Host/Experience/Tables
echo done > evidence/V1-RMD-802/$L-batch.done
