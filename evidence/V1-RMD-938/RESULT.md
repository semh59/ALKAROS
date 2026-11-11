# V1-RMD-938 Result

Status: Done.

- Each applied payment line carries its method (Cash, Card, Eft, Account) from the shared PaymentMethodLookup (the receipt and the refund flow use the same definition); the screen prints the Turkish name before the amount. Payments that are not approved send an empty method and the screen shows "Tahsilat".
- Evidence: red.log (4 screen tests red), green.log (StaticApps full, 470 tests), green-server.log and full-server*.log (PaymentTenderHttpTests all green in chunks: B/E/T 25, A 36, rest 16), mutations.txt (3 mutants killed).
- Follow-ups opened: line status text from paymentStatus; one method dictionary on the payment page.
