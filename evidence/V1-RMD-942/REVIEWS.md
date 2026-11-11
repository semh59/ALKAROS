# V1-RMD-942 Reviews

## qa-ux-tasarim
- Medium: the hint sat below the sticky button and could be off screen -> moved above the button, right under the fields.
- Low: no link to the button -> aria-describedby added (M5).
- Low: weak emphasis of the muted hint style -> accepted, same style as the method hint.

## qa-test-kalitesi
- Medium: no test for the hint clearing after a customer is chosen, and none for the button state on the account path -> added (M4 kills it).
- Low: switching back to the transfer shows its sentence again -> asserted; busy and stale-summary states have their own existing notices.
