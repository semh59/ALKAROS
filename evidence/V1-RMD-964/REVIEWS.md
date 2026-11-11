# V1-RMD-964 Reviews

## ui-turkce-metin-uzmani
- The plural-stripping pattern is gone everywhere in src.
- Medium: the same root cause remained in the success message ("Vergi profilleri kaydı oluşturuldu.") and in the selected-record card -> both now use the singular label (M3, M4).
- Low: the dictionary name catalogAddLabels no longer says only "add" -> left as is, renaming is cosmetic.

## qa-test-kalitesi
- Low: the edit title branch is not in this test (unchanged, covered by the product edit test); the expected titles repeat the dictionary on purpose, so a misspelled label would still be caught by a reader.
- All six tabs are covered.
