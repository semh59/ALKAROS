import subprocess, sys, os
R='D:/PROJECT/ALKAROS'
SP=os.environ['TEMP']+'/g5mut801'; os.makedirs(SP,exist_ok=True)
BL='tests/Host/Experience/Billing'
OC='FullyQualifiedName~OrderCancellationHttpTests|FullyQualifiedName~BillCancellationHttpTests'
DOM='tests/Modules/Orders/OrderAggregate'; DOMF='FullyQualifiedName~OrderCancelWholeTests'
ST='src/Host/Experience/Orders/OrderCancellation/OrderCancellationStore.cs'
EF='src/Host/Experience/Orders/OrderItemVoidEffects.cs'
BC='src/Host/Experience/Billing/BillCancellation.cs'
PORT='src/Modules/Tables/TableRelease/PostgresTableReleasePort.cs'
EP='src/Host/Experience/Orders/OrderManagementEndpoints.cs'
GUARD='src/Host/Experience/Billing/BillMoneyGuard.cs'
REPO='src/Modules/Billing/BillFoundation/PostgresBillRepository.cs'
ORD='src/Modules/Orders/OrderAggregate/Order.cs'
mutants=[
 ('m1-started-lines-restore-stock',EF,'var kitchenStarted = item.KitchenState is KitchenState.Preparing or KitchenState.Ready;','var kitchenStarted = false;',BL,OC),
 ('m2-portions-never-returned',EF,'        if (stockRestored)\n            await portions.ReturnPortionsAsync','        if (stockRestored && item.Quantity < 0)\n            await portions.ReturnPortionsAsync',BL,OC),
 ('m3-no-money-check',ST,'            throw new OrderCancelHasPaymentException(orderId);','            { }',BL,OC),
 ('m4-no-served-check',ST,'            throw new OrderCancelHasServedItemsException(orderId);','            { }',BL,OC),
 ('m5-bills-not-cancelled',ST,'var billsCancelled = await bills.CancelOpenBillsOfOrderAsync(\n            orderId, reason, actorId, now, connection, transaction, cancellationToken);','await bills.GetByOrderIdAsync(orderId, cancellationToken); var billsCancelled = 0;',BL,OC),
 ('m6-table-not-released',ST,'        await tables.ReleaseOrderAsync(orderId, "Sipariş iptal edildi", connection, transaction, cancellationToken);','',BL,OC),
 ('m7-no-audit',ST,'        await auditEvents.AppendAsync(','        if (reason.Length < 0) await auditEvents.AppendAsync(',BL,OC),
 ('m8-wrong-permission',EP,'context, terminalId, dualStore, authorization, ApplicationPermissions.BillsCancel, cancellationToken);\n            var result = await replays','context, terminalId, dualStore, authorization, ApplicationPermissions.BillsSplit, cancellationToken);\n            var result = await replays',BL,OC),
 ('m9-order-flag-ignored',BC,'if (cancelOrder && orderId is { } ownOrderId)','if (false && orderId is { } ownOrderId)',BL,OC),
 ('m10-bill-pointer-not-cleared',BC,'                await _tables.ClearBillPointerAsync(billId, connection, transaction, cancellationToken);','',BL,OC),
 ('m11-sent-check-not-forgotten',PORT,'"DELETE FROM table_mgmt.table_sent_checks WHERE order_id = @order_id;"','"SELECT 1 WHERE @order_id IS NULL;"',BL,OC),
 ('m12-items-not-superseded',REPO,'            SET superseded_at = @timestamp\n            WHERE superseded_at IS NULL AND bill_id IN (SELECT bill_id FROM cancelled)','            SET superseded_at = superseded_at\n            WHERE superseded_at IS NULL AND bill_id IN (SELECT bill_id FROM cancelled)',BL,OC),
 ('m13-domain-served-allowed',ORD,'        if (_items.Any(item => item.Status == OrderItemState.Active && item.KitchenState == KitchenState.Served))\n            throw new InvalidOperationException($"Order {Id} has served items and cannot be cancelled.");','',DOM,DOMF),
 ('m14-paid-bill-not-money',GUARD,"(ob.status = 'Paid' OR (","(ob.status = 'NoSuchState' OR (",BL,OC),
 ('m15-money-ignores-inflight',GUARD,"WHERE p.bill_id = {billId} AND p.status IN ('Pending', 'Unknown', 'ReconciliationRequired'))","WHERE p.bill_id = {billId} AND p.status IN ('NoSuchState'))",BL,OC),
 ('m16-reason-not-recorded',REPO,'cancellation_reason = @reason,','cancellation_reason = NULL,',BL,OC),
 ('m17-order-bill-lock-skipped',GUARD,'        foreach (var billId in billIds.Where(lockedBillIds.Add))\n            await LockBillAsync(','        foreach (var billId in billIds.Where(lockedBillIds.Add))\n            if (billId == Guid.Empty) await LockBillAsync(',BL,'FullyQualifiedName~OrderCancellationHttpTests'),
]
only=sys.argv[1:]
for label,f,old,new,proj,flt in mutants:
    if only and label.split('-')[0] not in only: continue
    o=f'{SP}/{label}.old'; n=f'{SP}/{label}.new'
    open(o,'wb').write(old.encode()); open(n,'wb').write(new.encode())
    subprocess.run(['sh',f'{R}/evidence/V1-RMD-801/mutate.sh',label,f,o,n,proj,flt],cwd=R)
    d=f'{R}/evidence/V1-RMD-801/mutation-{label}'
    print(label, open(d+'/result.txt').read().strip()[:110] if os.path.exists(d+'/result.txt') else 'NO RESULT', 'restored' if os.path.exists(d+'/restored.txt') else 'NOT RESTORED', flush=True)
