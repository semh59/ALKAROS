# V1-RMD-951 Reviews

## qa-api-uclar
Clean. Direct reads of module tables from the Host follow the existing pattern; the decision permission filter already guards the list; unknown subject types leave the label null; at most two queries, no N+1; the label holds only bill number, table number and product name (no personal data).
- Low: the SQL and prefixes are inline for the two subject types; a new subject type must be added by hand (accepted, two types only).

## qa-test-kalitesi
- Medium: no tests for a subject id with no row, an unknown subject type, or two grants on one bill -> one server test added covering all three.
- Low: long product name not tried (no length rule; the card wraps) -> left out.
- Low: the two-case client test mixed two behaviours -> split in two, the first checks two cards each carry their own line.
