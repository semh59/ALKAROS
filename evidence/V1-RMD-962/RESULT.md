# V1-RMD-962 Result

Status: Done.

- The cashier ticket title shows "Sipariş 14:30" (creation time in restaurant time) instead of the POS-... code; an unknown format shows "Sipariş <code>".
- Tests: routes folder 246/246 plus 4 case rows; 5 mutants killed.
- Open follow-ups: the raw code still shows on the customer display (CustomerDisplay.tsx), pending checks (PendingChecksWorkspace.tsx) and hanging orders (HangingOrdersPanel.tsx); a table order has no table name in the title.
