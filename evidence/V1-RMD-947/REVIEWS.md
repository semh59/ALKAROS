# V1-RMD-947 Reviews

## qa-frontend (verification of the assessment)
Valid, no counter evidence. The busy flag is cleared and the page drawn before window.print; a second press while the request runs finds the button disabled; the print style hides everything but the sheet and the sheet is hidden on screen; no afterprint cleanup is needed. Open product note: there is no silent printing setup in the repo, so every print opens the browser dialog (a browser launch flag on the till, not a code change).

## qa-test-kalitesi
- Medium: the aria-busy assertion was empty -> removed; the button state is the assertion (M1).
- Medium: no double press test -> added (M3).
- Low: print spy leak -> removed after each test; only the first call was seen -> the call count is asserted.
