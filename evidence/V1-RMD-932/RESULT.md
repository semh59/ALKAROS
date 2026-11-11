# V1-RMD-932 Result

With no card terminal registered (Token terminal record absent): the payment screen no longer draws the card method, and the server refuses a card tender with 409 CARD_TERMINAL_NOT_CONFIGURED without creating a reconciliation record, so the bill is never locked and the rest can be paid by cash or EFT. With a terminal registered the existing flow is unchanged. The tender summary now carries cardTerminalConfigured; an older server without the field keeps showing the card method.
Red: new server and client tests failed. Green: PaymentTender 119 tests (31 + 88), CashSession card test, StaticApps 41 files / 459 tests. Mutants: 6 killed, restored (cmp ok).
Note: the whole PaymentTender project takes about 13 minutes; a single full run hit the host hang limit, so it was run in two filtered halves.
