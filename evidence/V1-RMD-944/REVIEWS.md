# V1-RMD-944 Reviews

## qa-ux-tasarim
- Medium: inside a payment or request row the sentence would squeeze next to the button -> the row wraps and the sentence takes a full line (style test, M6).
- Clean: Turkish, no focus or caret loss (nodes are added and removed without a re-render), no pile-up, aria link, confirm step.

## qa-test-kalitesi
- Medium: no test for the busy state -> added (M5 kills it).
- Medium: only the forward direction -> the text is cleared again, the item quantity is emptied and restored, and one sentence per button is counted.
- Medium: one row only -> two payment rows, unique ids, each button linked to its own sentence.
- Low: weak attribute assertions -> replaced by hasAttribute and the role and class checks.
