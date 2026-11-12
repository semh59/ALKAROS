# V1-RMD-985 Result

The cashier cart no longer shows a finished order. A snapshot that is completed, cancelled, or fully paid (paid > 0 and remaining 0) opens as no order; the first product tap then opens a new order and never touches the finished one. A partly paid or unpaid order is still shown. The customer display is unchanged (it uses its own endpoint).

- red.log: 3 of 4 fail before the fix. green.log: 6 of 6 pass.
- related.log: all Cashier route tests pass (81 before the last test was added). tsc clean.
- mutations.txt: 4 mutations (M4 re-run with its own test), all killed, files restored (cmp).
