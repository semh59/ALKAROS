# V1-RMD-924 result

Report finding: the item/person split screen (/billing) could not be opened from any screen; the payment page offered only "Eşit bölüştür". The screen opens a bill through ?billId=, so the fix is a link on the payment page: "Ürüne veya kişiye göre böl" -> /billing?billId=<bill> (hidden while a card payment waits for reconciliation, like the form).
Red: link test failed (no link). Green: split-payment tests 20 files / 193 tests; 2 mutants killed.
Whole StaticApps suite: 447/448; the one failure (error-dictionary-lock: DISCOUNT_AFTER_REFUND has no screen sentence) is not caused by this change and is fixed as its own task.
