# V1-RMD-926 result

Report finding: closing the shift with an unpaid sale did not warn. Cause (verified in CashCloseStore.UnsettledSql): only bill rows were counted; an order sent to the till whose check was never opened has no bill row. The server already had the "warn, acknowledge, close" mechanism.
Fix: the open-check count also includes orders in a live status (Submitted, Accepted, Preparing, Ready, Served; the list the bill sweeper uses) that are not Online (paid on the platform) and have no non-cancelled bill. Warning text and acknowledgement flow unchanged.
Red: preview counted 1 instead of 3 (red.log). Green: whole CashCloseSafety class 27/27 (green.log, 3 min). 4 mutants killed (mutations.txt).
Notes: the audit event cash.session.closed keeps its field name openBills, which now also covers unbilled sent orders (earlier records counted bills only). PendingConfirmation QR orders are not counted (not accepted yet). Open: the end-of-day report counts bills only (own follow-up task).
