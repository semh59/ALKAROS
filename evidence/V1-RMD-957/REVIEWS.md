# V1-RMD-957 Reviews

## qa-frontend
- Medium: the text said "no bill to collect" while Hesabı al was shown for a table that only has a bill -> the text now follows the same rule as the button (a check exists and the user may open it).
- Low: text promised a bill to a user without the bill permission -> now says there is nothing they can do there; tested with no buttons.
- Low: the actions row can be empty for such a user -> accepted, harmless.

## qa-test-kalitesi
- Medium: non-manager on an empty table, and the table-card hint for a non-manager -> test added (M1, M3).
- Low: non-manager without the bill permission -> test added (M2, M4).
