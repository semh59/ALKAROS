# V1-RMD-940 Reviews

## qa-frontend
Clean: the button-style dictionary is used only for the method buttons. The server sends "Card" (PaymentMethodLookup.Card), matching the shared names.

## qa-test-kalitesi
- Medium: the refund test searched the whole page text -> it now reads the method name of every refund row and compares the list exactly (M3 kills a separate refund dictionary).
- Low: the payment line test is strict (starts with the name); receipt wording is produced by the server and was not changed.
