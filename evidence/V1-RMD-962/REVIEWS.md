# V1-RMD-962 Reviews

## qa-ux-tasarim
- Verified: the code time is in restaurant time (Europe/Istanbul). A till holds one editable order at a time, so two same-minute titles are not on screen together.
- Medium, not changed here: the raw code still shows on the customer display, the pending checks list and the hanging orders panel; a table order has no table name in the title.
- Low: the helper sat between a comment and its function -> moved.

## qa-test-kalitesi
- Medium: only a 28-character example and no midnight case -> real 32-character form and 00:05 added (M4, M5); legacy and short forms tested.
- Low: weak "no POS-" assert -> replaced by exact title per case.
