# V1-RMD-960 Reviews

## qa-backend
- Verified in code: the till sells on current_price IS NOT NULL (plus active and available); the recompute job clears current_price only for products whose dated pricing has started and has none effective now, so a seed price stays valid.
- Medium: a later-starting dated price hid the whole note -> the not-sellable sentence now depends only on the product price, not on the list (M3).
- Low: the sentence said "geçerli" for a product that may be inactive -> the word was dropped; it states the product-record price.
- Not changed: the timeline does not mark which price is effective now (separate gap, noted).

## qa-test-kalitesi
- Medium: later-starting price with and without a product price; zero price; each branch -> five cases in the parametrized test (M1-M4).
- Low: the note is asserted inside the whole detail text -> accepted.
