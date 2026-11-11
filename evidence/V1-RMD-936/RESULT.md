# V1-RMD-936 Result

Status: Done.

- The bill state refusal shows the server's sentence for the action (split, discount, tip, undo, cancel); the fixed split sentence is gone from the cashier dictionary.
- Review finding fixed: a tip refused on a settled bill used the discount-only sentence; the server sentence for discount and tip now reads "Bu hesap durumunda indirim ya da bahşiş uygulanamaz." (BillingSplitApplication.cs), asserted by two server tests.
- The lock test now proves the code is still worded per action (at least 4 server sentences) and a tip screen test covers the tip path.
- Evidence: red.log, red-server.log (2 server tests red), green.log (StaticApps 41 files / 462 tests), green-server.log (2 green), mutations.txt (6 mutants killed).
- Open follow-up: PosTerminal billingApi.ts still has one generic sentence for this code (Low).
