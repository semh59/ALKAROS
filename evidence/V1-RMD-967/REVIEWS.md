# V1-RMD-967 Reviews

## qa-api-uclar
- Clean: the permission filter is on the group, error mapping (404 item, 404 location, 409 inactive), the PUT is a full replacement with a single field, the allowlist reason matches the sibling entries.
- High (accepted as the main finding): balances and reservations stay at the location they were booked at, so changing the default location of an item that holds stock would make it read as out of stock at the new location -> the endpoint now refuses (409 STOCK_LOCATION_LOCKED) when the item has a balance row and the requested location differs; same location is accepted (M10, M11, M13).
- Low: a concurrent edit gives the usual 409 CONCURRENCY_CONFLICT; a passive item can have its location changed (no stock movement involved) -> left.

## qa-test-kalitesi
- Medium: inactive location at HTTP level -> test added (409 INACTIVE_RESOURCE, old location kept; helper got an isActive flag).
- Medium: unauthorized with an existing item, concurrent PUTs, other fields kept for a passive item -> the group filter answers 403 before the item is read (existing pattern); the other fields are kept by Update (reorder point asserted, M5); concurrency left.
- Medium: client error path and missing permission -> test added (server sentence shown as is, no button without inventory.manage).
- The stock-holding rule is covered by its own HTTP test.
