# V1-RMD-955 Reviews

## qa-ux-tasarim
- Medium: the first hint implied the count can be abandoned; it cannot (a started count stays until the drawer is closed) -> hint now says it cannot be undone once started.
- E2E specs find the button by the substring "Sayıma Başla" (no exact match), so the longer label still matches; "Kasayı Kapat" exists only on the later confirmation screen, so no double match.

## qa-test-kalitesi
- Medium: mutual exclusion of the two hints was tested in one direction -> both directions asserted.
- Low: exact-text assert brittle -> a fragment is checked. The busy label was never tested (not changed here).
