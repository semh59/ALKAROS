import fs from 'node:fs';
import { execSync } from 'node:child_process';
const OR = 'src/Modules/Orders/OrderAggregate/Order.cs';
const KC = 'src/Modules/Orders/Integration/KitchenEventOrderConsumer.cs';
const TS = 'src/Host/Experience/Orders/TableDraft/TableDraftService.cs';
const QX = 'src/Host/Experience/Orders/PendingOrderConfirmation/QrOrderExpiryHostedService.cs';
const KT = 'tests/Modules/Orders/OrderAggregate --filter FullyQualifiedName~KitchenEventOrderConsumerTests';
const TT = 'tests/Host/Experience/Orders/TableDraft --filter FullyQualifiedName~CheckLifecycleHttpTests';
const XT = 'tests/Host/Experience/Orders/Confirmation --filter FullyQualifiedName~QrOrderExpiryHostedServiceTests';
const M = [
  ['M1-backwards-allowed', OR, 'return next > current;', 'return next != current;', KT],
  ['M2-held-released-by-queued', OR, 'if (current == KitchenState.Held && next == KitchenState.Sent)\n            return false;', 'if (current == KitchenState.Held && next == (KitchenState)99)\n            return false;', KT],
  ['M3-unknown-state-not-logged', KC, 'LogUnmirrorable(_logger, $"unknown item state \'{e.ItemState}\'", e.OrderId, e.OrderItemId, null);', ';', KT],
  ['M4-missing-order-not-logged', KC, 'LogUnmirrorable(_logger, "order not found", e.OrderId, e.OrderItemId, null);', ';', KT],
  ['M5-missing-item-not-logged', KC, 'LogUnmirrorable(_logger, "order has no such item", e.OrderId, e.OrderItemId, null);', ';', KT],
  ['M6-inactive-table-claimed', TS, '                  AND t.active\n', '', TT],
  ['M7-out-of-service-table-claimed', TS, "                  AND t.current_status <> 'OutOfService'\n", '', TT],
  ['M8-live-order-pointer-overwritten', TS, "o.status NOT IN ('Completed', 'Cancelled', 'Rejected')", "o.status IN ('Draft', 'Submitted')", TT],
  ['M9-poison-order-stops-the-pass', QX, 'catch (Exception ex) when (!cancellationToken.IsCancellationRequested)', 'catch (Exception ex) when (cancellationToken.IsCancellationRequested)', XT],
  ['M10-poison-order-not-logged', QX, 'LogOrderFault(logger, orderId, ex);', '_ = (logger, orderId, ex);', XT],
];
const only = process.argv[2];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
for (const [id, file, from, to, target] of M) {
  if (only && only !== id) continue;
  const orig = fs.readFileSync(file, 'utf8');
  const crlf = orig.includes('\r\n');
  const f = crlf ? from.replace(/\n/g, '\r\n') : from;
  const t = crlf ? to.replace(/\n/g, '\r\n') : to;
  if (!orig.includes(f)) { fs.appendFileSync('evidence/V1-RMD-723/mutations.log', `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(f, t));
  let verdict;
  try { execSync(`${process.env.HOME}/.dotnet/dotnet.exe test ${target} -nologo`, { stdio: 'pipe', env }); verdict = 'SURVIVED'; }
  catch (e) { verdict = /error CS|error CA/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync('evidence/V1-RMD-723/mutations.log', `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync('evidence/V1-RMD-723/mutations.log', 'DONE\n');
