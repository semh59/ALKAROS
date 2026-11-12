# V1-RMD-999 Result

The Masalar screen is now the table plan: zone tabs (own row, filled active tab), search, status icon + pattern + colour for seven states, card with amount, age, waiter and party size, attention strip (one button per table: Hesap istedi, Odeme bekliyor), icon legend, details panel using the same status. The grid is the default view; the floor plan stays an option. After signing in on "/" an operator with tables.status lands here.
"Hesap istedi" = a check at the till with a table id and no bill (awaiting-payment list joined by table id). Not read for operators without orders.create; if it cannot be read, a warning is shown instead of silently hiding it.
Paket button omitted: the product has no takeaway flow. Floor plan drawing does not yet show the new status icons (follow-up inside 1001).
Evidence: red.log, red-route.log, red-landing.log (failing before), client-all.log 1250/1250 tests, lint.log, tsc.log, build.log all exit 0, mutations.txt (M1-M14 killed, restored).
Changed existing test expectations (legitimate): search test now checks card titles (the attention strip lists table numbers globally), floor plan route tests click the Salon plani toggle and use zone tabs, the card no longer shows the raw order id line.
