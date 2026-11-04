#!/bin/sh
cd /d/PROJECT/ALKAROS
R=evidence/V1-RMD-794/794-run.sh
sh $R g-handler tests/Modules/Cash/TenderHandler
sh $R g-tender tests/Host/Experience/PaymentTender
sh $R g-cashsession tests/Host/Experience/CashSession
sh $R g-apiconv tests/Architecture/ApiConventions
sh $R g-bounds tests/Architecture/ModuleBoundaries
sh $R g-errors tests/Host/Errors
echo done > evidence/V1-RMD-794/green.done
