#!/bin/sh
cd /d/PROJECT/ALKAROS
R=evidence/V1-RMD-794/794-run.sh
sh $R w-tender tests/Host/Experience/PaymentTender
sh $R w-account tests/Host/Experience/CustomerAccounts
sh $R w-cashsession tests/Host/Experience/CashSession
sh $R w-handler tests/Modules/Cash/TenderHandler
echo done > evidence/V1-RMD-794/last.done
