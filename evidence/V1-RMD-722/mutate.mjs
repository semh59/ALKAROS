import fs from 'node:fs';
import { execSync } from 'node:child_process';
const SV = 'src/Host/Experience/Orders/SentItemVoid/SentItemVoidStore.cs';
const RP = 'src/Host/Experience/Orders/OrderCommandReplayStore.cs';
const EP = 'src/Host/Experience/Orders/OrderManagementEndpoints.cs';
const RS = 'src/Host/Experience/Orders/OrderReadStore.cs';
const VS = 'tests/Host/Experience/Orders/VoidSent';
const CP = 'tests/Host/Experience/Orders/Comp';
const TD = 'tests/Host/Experience/Orders/TableDraft --filter FullyQualifiedName~ServerHandOff';
const M = [
  ['M1-kitchen-step-skipped', SV, 'kitchenCancelled = await CancelKitchenTicketItemAsync(', 'kitchenCancelled = false && await CancelKitchenTicketItemAsync(', VS],
  ['M2-item-event-after-commit', SV, "await AppendItemEventAsync(connection, transaction, item, command, kitchenCancelled, wasteConverted, cancellationToken).ConfigureAwait(false);\n            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);", "await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);\n            await AppendItemEventAsync(connection, transaction, item, command, kitchenCancelled, wasteConverted, cancellationToken).ConfigureAwait(false);", VS],
  ['M3-different-request-accepted', RP, 'if (found.RequestHash != requestHash)', 'if (false)', VS],
  ['M4-replay-lookup-off', RP, 'var stored = await FindAsync(connection, scope, key, cancellationToken).ConfigureAwait(false);', '(string RequestHash, byte[] Body)? stored = null;', VS],
  ['M5-closed-order-comp-allowed', EP, 'if (order.Status is OrderState.Completed or OrderState.Cancelled or OrderState.Rejected)', 'if (false)', CP],
  ['M6-paid-bill-comp-allowed', EP, 'bill.Status is not (BillState.Open or BillState.Reopened))\n                throw new BillNotModifiableForWasteException(bill.Id, bill.Status.ToString());\n        }\n    }\n\n    private static async Task<Guid> RequireCashierSessionAsync', 'false)\n                throw new BillNotModifiableForWasteException(bill.Id, bill.Status.ToString());\n        }\n    }\n\n    private static async Task<Guid> RequireCashierSessionAsync', CP],
  ['M7-transfer-not-audited', RS, 'eventName: "Order.ServerTransferred",', 'eventName: "Order.Unrecorded",', TD],
  ['M8-transfer-commits-before-audit', RS, "var count = await _repository.ReassignServingUserAsync(fromUserId, toUserId, connection, transaction, cancellationToken);", "var count = await _repository.ReassignServingUserAsync(fromUserId, toUserId, cancellationToken);", TD],
];
const only = process.argv[2];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
for (const [id, file, from, to, target] of M) {
  if (only && only !== id) continue;
  const orig = fs.readFileSync(file, 'utf8');
  const crlf = orig.includes('\r\n');
  const f = crlf ? from.replace(/\n/g, '\r\n') : from;
  const t = crlf ? to.replace(/\n/g, '\r\n') : to;
  if (!orig.includes(f)) { fs.appendFileSync('evidence/V1-RMD-722/mutations.log', `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(f, t));
  let verdict;
  try { execSync(`${process.env.HOME}/.dotnet/dotnet.exe test ${target} -nologo`, { stdio: 'pipe', env }); verdict = 'SURVIVED'; }
  catch (e) { verdict = /error CS|error CA/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync('evidence/V1-RMD-722/mutations.log', `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync('evidence/V1-RMD-722/mutations.log', 'DONE\n');
