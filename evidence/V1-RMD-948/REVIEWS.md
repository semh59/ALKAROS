# V1-RMD-948 Reviews

## qa-frontend
Clean. The session answer carries the permission list; the server reset endpoint asks for the same permission from the same source; supervisors and managers hold it, cashiers and waiters do not; there is no other override path, so an authorized user never loses the card. The list is read once at open (a stale card cannot do harm because the server checks again).

## qa-test-kalitesi
- Medium: a missing or null list and a similar looking code were not tried -> added, plus the list must be an array (M3).
- Medium: only the settled page was covered -> the unpaid page with an authorized session shows no card.
- Low: the authorized case was only implicit -> a direct test; no server call and the print button present in the unauthorized cases.
