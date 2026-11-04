#!/bin/sh
cd /d/PROJECT/ALKAROS
sh evidence/V1-RMD-794/final.sh
sh evidence/V1-RMD-794/mut3.sh
R=evidence/V1-RMD-794/794-run.sh
sh $R z-handler tests/Modules/Cash/TenderHandler
sh $R z-account tests/Host/Experience/CustomerAccounts
sh $R z-tender tests/Host/Experience/PaymentTender
sh $R z-cashsession tests/Host/Experience/CashSession
sh $R z-apiconv tests/Architecture/ApiConventions
sh $R z-bounds tests/Architecture/ModuleBoundaries
sh $R z-errors tests/Host/Errors
echo done > evidence/V1-RMD-794/all.done
