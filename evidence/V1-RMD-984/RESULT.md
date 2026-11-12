# V1-RMD-984 Result

Product decision: a table order without a bill stays out of Bekleyen hesaplar (unchanged). Once the cashier opens its bill (Hesabı al) the table-bound order is listed, with its bill and amounts. Cancelled and Paid bills keep it out.
Recalling such an order to its table also cancels the bill and now clears the table's bill pointer (review finding).

- red.log, red2.log: new tests fail before the fix. green.log: MergedBillOrderLinkTests 18 of 18 pass.
- related.log: TableDraft CheckLifecycle and OrderManagementTableDraft HTTP tests 119 of 119 pass.
- mutations.txt: 4 mutations, all killed, files restored (cmp).
