# V1-RMD-937 Result

Status: Done.

- Enter in the opening amount field now runs the same validation and submit as the "Kasayı Aç" button (a disabled button ignores the click, so there is no double submit).
- Evidence: red.log (1 red), green.log (StaticApps full run green), mutations.txt (2 mutants killed); tests: Enter sends one POST, invalid amount and other keys send none.
