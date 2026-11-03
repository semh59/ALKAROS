# V1-RMD-792 Result

Cash close screen (Cashier static page), manager end-of-day section and settings labels now follow the server rules of V1-RMD-790 and V1-RMD-791.

## What changed
- Cash close page: warns about open bills and unsettled payments, close stays off until "Bunları bilerek kapatıyorum" is ticked; a non-zero difference needs an explanation before any request is sent; when the server says the tolerance is exceeded one manager block opens (no second error box) and the same text goes out as both reasons; the tolerance shown is the server value; six new error codes have Turkish text; "Süpervizör" became "Yönetici"; `aria-live` removed from the whole page (only error and notice are live regions).
- Manager end-of-day section: the two hand-typed number fields are gone; the screen reads `close-preflight` (revenue without tips, tips on their own line, orders, cancelled items, print failures, open cash sessions, open bills, unsettled payments); the close button is off while the preflight is loading/failed and, when open items exist, until the warning checkbox is ticked; the call sends only `acknowledgeOpenItems`. A closed day shows tips, open-item counts, table breakdown, cash reconciliation and print summary.
- Settings: "Kasa kapanış fark toleransı" (0-500 TL, rule text) and the self-approval label now also names the cash reconciliation.
- E2E: spec 05 follows the explanation flow; spec 11 (outside the first scope, added by a plan commit) was already broken by an earlier table-form change (option text without trailing space, "Açıklama" field replaced by a preset reason) and is fixed.

## Evidence
- Red first: vitest-red.log (cash page, 15 of 17 new tests red on HEAD sources), vitest-ps-red.log (end-of-day, 5 red), vitest-settings-red.log (2 red); table number backend test did not compile before the change.
- Green: cash page files 87/87 earlier, StaticApps whole suite 235/235, PosTerminal whole suite 855/855 (closing + settings 47/47), tsc clean, oxlint clean on touched files. Backend: Host Reporting 30/30, V1Operations 15/15.
- E2E (real Host + Postgres + Chromium): e2e.log, 83/83, including the new open-bill scenario in spec 05.
- Mutation: mutation-summary.txt, 24 mutants, all killed (first-round survivors fixed by tests or by removing redundant lines, then re-run).
- Reviews: qa-frontend (7 findings, 6 fixed, 1 accepted: client copy of the 500 limit mirrors the settings screen pattern), qa-test-kalitesi (8 findings: all fixed except a browser E2E of the manager end-of-day screen, covered by component tests and real-Postgres backend tests), verification round: no findings.
- English leak scan (UI_STYLE_GUIDE section 4) on the diff: no screen text.
- Skipped tests: none.

## Manual scenario (Semih)
1. Open a bill and leave it unpaid, open the drawer, count, go to the close screen: yellow warning with the counts, close is off until the box is ticked.
2. Count 40 TL short: close asks for an explanation; with a difference above the tolerance a single manager block appears.
3. Management > Gün sonu: no number fields; summary comes from the server; with open items the close needs the tick.
4. Settings: change "Kasa kapanış fark toleransı"; 501 is refused.
