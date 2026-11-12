# V1-RMD-988 Result

The end-of-day report SQL holds three copies of the bill-order link (V1-RMD-935 added the third, open-order check). The copies test now covers all three and a separate test pins the copy count and the cancelled-bill condition of the third. The two failing tests on master are green (BillOrderLinkCopiesTests 7 of 7).

- red.log: 2 of 5 failing before. green.log: 7 of 7 pass.
- mutations.txt: 3 mutations killed (M1 third copy loses the order condition, M2 old count, M3 cancelled-bill condition dropped), files restored.
