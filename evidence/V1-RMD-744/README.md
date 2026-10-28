# V1-RMD-744 evidence

Fix: a replay (same IdempotencyKey) of a discount or tip is answered before the bill-state check, so a retry after the bill was paid returns the original adjustment instead of 409; a new key is still refused. The discount endpoint's pre-check (`EnsureDiscountRequestableAsync`) also lets an already-applied key through.

- `green.log`: Billing host tests on real PostgreSQL (port 55432), 108/108, exit 0 (`green-filtered.log`: the discount/replay subset).
- `red1.log`, `red2.log` + `mutations.log`: re-rejecting replays in the discount gate, and checking the tip state before the replay lookup, both fail the new tests (Expected OK / Actual Conflict); sources restored and verified with `cmp`.
