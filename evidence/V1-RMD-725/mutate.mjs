import fs from 'node:fs';
import { execSync } from 'node:child_process';
const EP = 'src/Host/Experience/Orders/OrderManagementEndpoints.cs';
const RS = 'src/Host/Experience/Orders/OrderReadStore.cs';
const PR = 'src/Modules/Orders/OrderAggregate/PostgresOrderRepository.cs';
const SH = 'src/Modules/Orders/SubmitOrder/SubmitOrderHandler.cs';
const TS = 'src/Host/Experience/Orders/TableDraft/TableDraftService.cs';
const CT = 'src/Clients/PosTerminal/src/routes/Cashier.tsx';
const PC = 'src/Clients/PosTerminal/src/features/pending-checks/PendingChecksWorkspace.tsx';
const NF = 'src/Clients/PosTerminal/src/routes/NfcOrder.tsx';
const PS = 'src/Clients/WaiterPwa/wwwroot/js/sheets/product-sheet.js';
const dn = (dir, filter) => ({ cmd: `${process.env.HOME}/.dotnet/dotnet.exe test ${dir} -nologo --filter "${filter}"` });
const vt = (cwd, file) => ({ cmd: `npx vitest run ${file}`, cwd });
const CONF = dn('tests/Host/Experience/Orders/Confirmation', 'FullyQualifiedName~OrderRequestRobustnessTests|FullyQualifiedName~ThePendingListIsCapped');
const AGG = dn('tests/Modules/Orders/OrderAggregate', 'FullyQualifiedName~PostgresOrderTests');
const TD = dn('tests/Host/Experience/Orders/TableDraft', 'FullyQualifiedName~TooManyModifiers|FullyQualifiedName~Handoff');
const SUB = dn('tests/Modules/Orders/SubmitOrder', 'FullyQualifiedName~ALostDeadlock');
const M = [
  ['M1-hangup-logged-as-error', EP, 'catch (OperationCanceledException) when (context.HttpContext.RequestAborted.IsCancellationRequested)', 'catch (OperationCanceledException) when (!context.HttpContext.RequestAborted.IsCancellationRequested)', CONF],
  ['M2-truncation-is-an-outage', EP, ' or PostgresErrorCodes.StringDataRightTruncation', '', CONF],
  ['M3-hub-failure-fails-the-request', EP, 'catch (Exception exception) when (exception is not OperationCanceledException)\n        {\n            LogNotifyFailed', 'catch (Exception exception) when (exception is OperationCanceledException)\n        {\n            LogNotifyFailed', CONF],
  ['M4-pending-list-uncapped', RS, 'LIMIT 200;', 'LIMIT 2000;', CONF],
  ['M5-history-no-tie-break', PR, 'ORDER BY changed_at, order_status_history_id;', 'ORDER BY changed_at;', AGG],
  ['M6-course-change-skipped', PR, '\n            && CourseNumber == item.CourseNumber;', ';', AGG],
  ['M7-item-time-not-advanced', PR, 'notes = @notes,\n                updated_at = now(),', 'notes = @notes,\n                updated_at = @updated_at,', AGG],
  ['M8-deadlock-retry-silent', SH, 'LogDeadlockRetry(_logger, command.OrderId, attempt, ex);', '_ = (_logger, command.OrderId, attempt, ex);', SUB],
  ['M9-modifier-count-unbounded', TS, 'item.Modifiers.Count > MaximumModifiersPerItem', 'item.Modifiers.Count > 5000', TD],
  ['M10-note-left-for-bad-target', EP, 'await store.EnsureTransferTargetAsync(request.FromUserId, request.ToUserId, cancellationToken);\n            await handoffNotes', 'await handoffNotes', TD],
  ['M11-help-ack-failure-silent', CT, 'setError(reason instanceof ApiError ? reason.message : "Çağrı onaylanamadı. Tekrar deneyin.");', 'void reason;', vt('src/Clients/PosTerminal', 'src/routes/Cashier.help-alerts.test.tsx')],
  ['M12-stale-read-wins', PC, 'if (ticket !== latestLoad.current) return;\n      if (hasNewCheck', 'if (hasNewCheck', vt('src/Clients/PosTerminal', 'src/features/pending-checks')],
  ['M13-picker-ignores-escape', NF, 'if (event.key === "Escape") {', 'if (event.key === "Never") {', vt('src/Clients/PosTerminal', 'src/routes/NfcOrder.test.tsx')],
  ['M14-zero-ceiling-means-unlimited', PS, 'Number.isInteger(group.maxSelections)', 'group.maxSelections > 0', vt('tests/Clients/StaticApps', 'waiter-app.test.js')],
];
const only = process.argv[2];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres', PATH: process.env.PATH + ';C:\Program Files\nodejs' };
for (const [id, file, from, to, target] of M) {
  if (only && only !== id) continue;
  const orig = fs.readFileSync(file, 'utf8');
  const crlf = orig.includes('\r\n');
  const f = crlf ? from.replace(/\n/g, '\r\n') : from;
  const t = crlf ? to.replace(/\n/g, '\r\n') : to;
  if (!orig.includes(f)) { fs.appendFileSync('evidence/V1-RMD-725/mutations.log', `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(f, t));
  let verdict;
  try { execSync(target.cmd, { stdio: 'pipe', env, cwd: target.cwd }); verdict = 'SURVIVED'; }
  catch (e) { verdict = /error CS|error CA/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync('evidence/V1-RMD-725/mutations.log', `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync('evidence/V1-RMD-725/mutations.log', 'DONE\n');
