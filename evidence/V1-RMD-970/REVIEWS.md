# V1-RMD-970 Reviews

## ui-turkce-metin-uzmani
- Verified in code: the suggestion is the counted cash (actual_cash) of this terminal's latest closed session; no "left in drawer" field exists -> the hint says "son kapanışta sayılan nakit" and asks to count the drawer and change if needed.
- Low, not changed: the field still starts at 0.00 when there is no suggestion, so a cashier could open without counting (separate UX question).

## qa-test-kalitesi
- Medium: a failed suggestion call reads as "no suggestion" -> accepted and pinned by a test row (counting the drawer is the right action either way).
- Low: a zero suggestion -> row added (M4); fragile selector -> the hint has an id (opening-hint).
