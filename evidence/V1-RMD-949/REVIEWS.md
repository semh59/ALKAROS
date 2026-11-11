# V1-RMD-949 Reviews

## qa-api-uclar
Clean. The mapping applies only to the stock item and location creation paths (the other inventory endpoints do not carry these parameter names; the waste unit field gets its correct sentence); the screen shows the server sentence as is; no API convention test is affected. Numeric type values are also refused now (IsDefined).

## qa-test-kalitesi
- Medium: the session answer must come before any field sentence -> test added (401 and 403 with an invalid body).
- Low: empty type, blank code, unknown unit, negative reorder point were not tried -> cases and a mapping added (unknown unit, reorder point; M4 and M5).
- Low: no record must be created on a refusal -> the list is checked after every case.
- Left out: very long fields (no length rule exists in the domain).
