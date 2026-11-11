# V1-RMD-963 Reviews

## qa-ux-tasarim
- Verified: terminalName is used only by the shell header; the doubled word is gone.
- Medium, not changed: the cashier screen (Cashier.tsx) prints "Terminal · <short id>" itself, so the technical id still shows there; the manager's terminals panel shows a short id too. A real fix needs a terminal name field and a naming screen (product decision, see RESULT).

## qa-test-kalitesi
- Medium: the doubled-word regex was weak -> the test reads the Terminal item and compares its value exactly (M1-M4).
- Medium: an empty terminal id would show an empty value -> not possible: the id comes from the signed-in session and the route needs it to load anything.
