#!/bin/sh
cd /d/PROJECT/ALKAROS
R=evidence/V1-RMD-802/802-run.sh
sh $R z-secadmin tests/Host/Experience/SecurityAdministration
sh $R z-paymenttender tests/Host/Experience/PaymentTender
sh $R z-cashsession tests/Host/Experience/CashSession
sh $R z-customeraccounts tests/Host/Experience/CustomerAccounts
sh $R z-arch tests/Architecture/ModuleBoundaries
sh $R z-billing tests/Host/Experience/Billing
echo done > evidence/V1-RMD-802/z-batch.done
