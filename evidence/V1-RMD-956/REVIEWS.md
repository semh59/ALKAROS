# V1-RMD-956 Reviews

## qa-frontend
- Medium: the first version scrolled on any redraw after a busy phase, even with an unchanged error (e.g. the 5 s confirmation poll) -> removed; submitTender and setError now forget the last shown error instead, so only a new attempt scrolls.
- Low: block "nearest" may leave the box under the sticky pay button, no scroll-margin; older Safari ignores the options object -> accepted (the box is at the top, the button at the bottom); real visibility cannot be proven in jsdom.
- Low: whole-page rewrite re-announces the alert on every render -> pre-existing, unchanged.

## qa-test-kalitesi
- Medium: scroll option not asserted -> asserted ("nearest" on every call, mutant M5).
- Medium: a repeated identical validation error did not scroll and was untested -> now scrolls (setError) and is tested (M3).
- Low: one long test, only two error sources (server rejection, client validation), error text not asserted -> accepted.
