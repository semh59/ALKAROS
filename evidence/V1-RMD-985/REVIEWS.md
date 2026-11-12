# V1-RMD-985 Reviews

## qa-uctan-uca-akis
No blocking finding. Note: the terminal's active_order_id on the server still points at the finished order until a new one starts; the client filter handles it, a server-side clear would be an architecture decision (not done).

## qa-frontend
Applied: test that the first tap after a clean cart posts a new order and never uses the old order id.
Open (existing before this task): the first-load/conflict reload path does not use the stale-answer counter; a slow reload after logout can overwrite the cleared state. A zero-total fully paid order (all complimentary) with paid 0 stays visible until completed.
