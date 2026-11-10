# V1-RMD-927 Reviews

## qa-test-kalitesi
Findings: lower bound weak (>=768), no upper bound, base rule not asserted. Fixed: threshold must be 800..900 and the base #submit-tender rule must stay sticky. Re-ran green 18/18. Open (accepted): test reads CSS text, no computed-style check in jsdom.

## qa-ux-tasarim
Direction correct. Notes accepted without change: the cut-off is viewport height not content height (a taller window with long content can still overlap; follow-up if seen on device); on-screen keyboard may flip sticky/static (device dependent, unverified); on short screens the button needs a scroll; scroll-padding-bottom is harmless and kept for tall screens.
