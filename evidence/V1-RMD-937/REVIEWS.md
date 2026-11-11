# V1-RMD-937 Reviews

## qa-frontend
Clean. Low: the test did not prove a single POST -> the test now presses Enter twice and expects exactly one POST.

## qa-test-kalitesi
- Missing negative test for another key -> added (M2 kills it).
- Missing invalid amount + Enter -> added.
- Busy Enter -> covered by the double Enter test (disabled button ignores the click).
- 409 choice only proves the request is sent; accepted, the success path is owned by the existing open tests.
