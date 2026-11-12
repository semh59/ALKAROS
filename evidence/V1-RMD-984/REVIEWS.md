# V1-RMD-984 Reviews

## qa-backend
Applied: recall of a table-bound order left the table pointing at the cancelled bill; now cleared in the same transaction (test + mutation M4).
Open, not in this task: (1) a waiter can still add lines to a table order after its bill is opened, so the list total can differ from the bill; (2) a split bill whose first part is paid drops the whole order from the queue (existing V1-RMD-279 behaviour, now also visible for table-bound orders); (3) the queue shows only the newest bill of an order.

## qa-test-kalitesi
Applied: cancelled and paid bill cases (theory), table-bound order without bill, mutations M2 and M3.
Not done: Draft-with-bill case (the Submitted filter is unchanged by this task).
