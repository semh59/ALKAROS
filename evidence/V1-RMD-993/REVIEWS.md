# V1-RMD-993 Reviews

## qa-backend
Race and atomicity checked clean (terminal row lock, same transaction as the table bind). Applied: the cancel now writes cancelled_at, updated_at and the history row like the aggregate does, with a test.
Open (low): a zero-row update leaves a stale status for the conflict message; every quick-sale start cancels an older empty draft (extra Cancelled orders); a reusable empty draft would be a product decision.

## qa-uctan-uca-akis
Applied: the conflict message names the sales screen and the real exits.
Open: the cashier sales screen has no one-tap "empty the cart" (items are removed one by one, 2 taps each) and the Tables error does not link to the sales screen; an empty table draft for another table still blocks (needs a table release, product decision).
