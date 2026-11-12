# V1-RMD-998 Result

GET tables now returns ServingWaiterName and PartySize of the table's current order (primary table's order on a merged table; none for no order, cancelled/completed order). Detail and mutation responses leave them null by design (the list is what the table plan polls). Hesap istedi needs no server field: it is joined from the awaiting-payment list by table id in the screen task 999.
Tests: Tables project 43/43 (green.log). The first test was written together with the code; its strength is shown by mutations M1-M5, all killed (mutations.txt).
