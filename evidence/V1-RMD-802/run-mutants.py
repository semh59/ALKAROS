import subprocess, sys, os
R = 'D:/PROJECT/ALKAROS'
E = R + '/evidence/V1-RMD-802'
SP = os.environ['TEMP'] + '/g5mut802'
os.makedirs(SP, exist_ok=True)
BL = 'tests/Host/Experience/Billing'
CL = 'FullyQualifiedName~OrderCleanupTests'
CLH = 'tests/Host/Experience/SecurityAdministration'
CLHF = 'FullyQualifiedName~HangingOrdersHttpTests'
STORE = 'src/Host/Experience/Orders/OrderCleanup/OrderCleanupStore.cs'
ADM = 'src/Host/Experience/SecurityAdministration/OrderBacklogAdministration.cs'
SWEEP = 'src/Host/Experience/Billing/BillClosureSweeper.cs'
CLOSE = 'src/Modules/Billing/PaymentClosure/BillClosureService.cs'
SPLIT = 'src/Host/Experience/Billing/BillingSplitStore.cs'
HUB = 'src/Host/Experience/WaiterNotifications/WaiterOrderStatusHub.cs'
ANN = 'src/Host/Experience/WaiterNotifications/SignalRCashierQueueAnnouncer.cs'
BSA = 'src/Host/Experience/Billing/BillingSplitApplication.cs'
OME = 'src/Host/Experience/Orders/OrderManagementEndpoints.cs'
DSS = 'src/Host/DualScreen/DualScreenStore.cs'
REPAIR = 'FullyQualifiedName~PaidOrderRepairTests'
LOCK = 'FullyQualifiedName~ClosureSettlementLockTests'
OCT = 'FullyQualifiedName~OrderCancellationHttpTests'
SPT = 'FullyQualifiedName~BillingSplitHttpTests'
mutants = [
 ('m1-recheck-skipped', STORE, 'if (await recheck.ExecuteScalarAsync(cancellationToken) is null)\n                return false;', 'await recheck.ExecuteScalarAsync(cancellationToken);', BL, CL),
 ('m2-items-ignored', STORE, "i.status IN ('Draft', 'Active'))", "i.status IN ('NoSuchState'))", BL, CL),
 ('m3-age-ignored', STORE, "o.updated_at < now() - @age", "o.updated_at < now() + @age", BL, CL),
 ('m4-user-actor', STORE, 'SystemActorId, "System", "empty-order-sweeper"', 'SystemActorId, "User", "empty-order-sweeper"', BL, CL),
 ('m5-active-bill-listed', STORE, "b.status <> 'Cancelled')", "b.status <> 'Open')", CLH, CLHF),
 ('m6-source-ignored', STORE, "AND o.source IN ('Cashier', 'Waiter')\n        AND o.created_at", "AND o.created_at", CLH, CLHF),
 ('m7-reason-not-required', ADM, 'if (string.IsNullOrEmpty(reason) || reason.Length > OrderCancellationStore.MaxReasonLength)', 'if (reason is null || reason.Length > OrderCancellationStore.MaxReasonLength)', CLH, CLHF),
 ('m8-dryrun-off-by-default', ADM, 'if (dryRun ?? true)\n                return Results.Ok(new ClearHangingOrdersResultV1', 'if (dryRun ?? false)\n                return Results.Ok(new ClearHangingOrdersResultV1', CLH, CLHF),
 ('m9-payment-outcome-hidden', STORE, 'catch (OrderCancelHasPaymentException) { return HangingOrderOutcome.HasPayment; }', 'catch (OrderCancelHasPaymentException) { return HangingOrderOutcome.Changed; }', CLH, CLHF),
 ('m10-grace-ignored', SWEEP, "COALESCE(b.closed_at, b.updated_at) < now() - @grace", "COALESCE(b.closed_at, b.updated_at) > now() - @grace", BL, REPAIR),
 ('m11-open-sibling-ignored', SWEEP, "b.order_id = o.order_id AND b.status NOT IN ('Paid', 'Cancelled'))\n                ORDER BY o.order_id", "b.order_id = o.order_id AND b.status NOT IN ('Paid', 'Cancelled', 'Open'))\n                ORDER BY o.order_id", BL, REPAIR),
 ('m12-closure-lock-skipped', CLOSE, 'await using var settlementLock = await _bills.AcquireSettlementLockAsync(billId, cancellationToken);', '', BL, LOCK),
 ('m13-pointer-not-attached', SPLIT, 'await _tables.AttachBillPointerAsync(tableId, billId, connection, transaction, cancellationToken);', '', BL, SPT),
 ('m14-queue-group-for-all', HUB, 'if (await _store.HasPermissionAsync(principal.UserId, OrdersCreatePermission, Context.ConnectionAborted))', 'if (true)', 'tests/Host/Experience/WaiterNotifications', ''),
 ('m15-announce-to-all', ANN, '_hub.Clients.Group(WaiterOrderStatusHub.CashierQueueGroup).SendAsync(', '_hub.Clients.All.SendAsync(', 'tests/Host/Experience/WaiterNotifications', ''),
 ('m16-bill-cancel-silent', BSA, '"BillCancelled"', '"Other"', BL, CL),
 ('m17-order-cancel-queue-silent', OME, 'AnnouncePendingChecksChangedAsync("Cancelled", orderId, null, cancellationToken);', 'AnnouncePendingChecksChangedAsync("Sent", orderId, null, cancellationToken);', BL, CL),
 ('m18-half-flag-dropped', DSS, 'AllowsHalfPortion: reader.GetBoolean(13)', 'AllowsHalfPortion: false', BL, CL),
 ('m19-second-lock-round-skipped', 'src/Host/Experience/Orders/OrderCancellation/OrderCancellationStore.cs', '        // A bill opened after the first locks is locked now, so the money check below sees it.\n        await BillMoneyGuard.LockOrderBillsAsync(connection, transaction, orderId, lockedBills, cancellationToken);\n', '', BL, OCT),
 ('m20-confirmed-ids-ignored', ADM, 'listed.Where(o => confirmed.Contains(o.OrderId)).ToList()', 'listed', CLH, CLHF),
 ('m22-ids-not-required', ADM, 'if (request?.OrderIds is not { Count: > 0 } confirmed)', 'if (request?.OrderIds is not { } confirmed)', CLH, CLHF),
 ('m21-age-floor-dropped', ADM, 'olderThanHours ?? 12, 6, 720', 'olderThanHours ?? 12, 1, 720', CLH, CLHF),
]
only = sys.argv[1:]
for label, f, old, new, proj, flt in mutants:
    if only and label.split('-')[0] not in only:
        continue
    o = f'{SP}/{label}.old'
    n = f'{SP}/{label}.new'
    open(o, 'wb').write(old.encode())
    open(n, 'wb').write(new.encode())
    subprocess.run(['sh', f'{E}/mutate.sh', label, f, o, n, proj, flt], cwd=R)
    d = f'{E}/mutation-{label}'
    print(label, open(d + '/result.txt').read().strip()[:110] if os.path.exists(d + '/result.txt') else 'NO RESULT',
          'restored' if os.path.exists(d + '/restored.txt') else 'NOT RESTORED', flush=True)
