# V1-RMD-934 Result

The shift close warning now counts sent orders that have no bill yet from every source, online included (live statuses Submitted, Accepted, Preparing, Ready, Served; draft, completed and cancelled orders and orders with a non-cancelled bill do not count).
Red: the test expecting the online order failed. Green: CashSession close safety class 27/27. Mutants: 4 killed (online excluded again, served / preparing / ready dropped), restored (cmp ok).
