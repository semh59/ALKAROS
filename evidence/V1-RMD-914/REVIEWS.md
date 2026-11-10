# V1-RMD-914 reviews

## qa-veritabani
- Numbering and registration complete (order.json, PhaseBMax, ManifestTests, schema test); up and down are idempotent; the two range queries (delete older than, existing days from) start with snapshot_date and can use the new index (reasoned from the code, no EXPLAIN run).
- CREATE INDEX without CONCURRENTLY: same accepted pattern as 041; one row per customer per day, low risk.
- ManifestTests comment still named 223 as the last entry: fixed.
- GetSnapshotDatesAsync has LIMIT 10000 without ORDER BY: pre-existing and harmless (at most about 100 distinct days in its window).

## qa-test-kalitesi
- The shape string covers column, non-unique and full; rollback and reapply covered; manifest mutants (PhaseBMax kept at 223, entry removed) were missing and are now killed (mutants 5 and 6).
