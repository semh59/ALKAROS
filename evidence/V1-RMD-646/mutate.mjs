// Mutation run for V1-RMD-646: apply one mutant, run the matching tests, restore, verify identical.
import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const ROOT = 'D:/PROJECT/ALKAROS';
const STORE = `${ROOT}/src/Modules/Audit/EventStore/PostgresAuditEventStore.cs`;
const IFACE = `${ROOT}/src/Modules/Audit/EventStore/IAuditEventStore.cs`;
const BILL = `${ROOT}/src/Host/Experience/Billing/BillCancellation.cs`;
const REF = `${ROOT}/src/Host/Experience/Billing/PaymentRefunds.cs`;
const LOG = `${ROOT}/evidence/V1-RMD-646/mutations.log`;
const AUDIT_T = `${ROOT}/tests/Modules/Audit/EventStore/ALKAROS.Audit.EventStore.Tests.csproj`;
const BILLING_T = `${ROOT}/tests/Host/Experience/Billing/ALKAROS.Host.Experience.Billing.Tests.csproj`;
const ONLY = process.env.ONLY ? process.env.ONLY.split(',') : null;
if (!ONLY) writeFileSync(LOG, '');
const files = [STORE, IFACE, BILL, REF];
const backups = Object.fromEntries(files.map((f, i) => [f, join(tmpdir(), `m646-${i}.orig`)]));
for (const f of files) copyFileSync(f, backups[f]);
const nl = (s) => s.replace(/\r\n/g, '\n');

const mutants = [];
const rep = (file, label, test, filter, from, to) => mutants.push([file, label, test, filter, (s) => { if (!s.includes(from)) throw new Error('no match: ' + label); return s.replace(from, to); }]);
const swapBeforeCommit = (file, label, test, filter, startMarker) => mutants.push([file, label, test, filter, (s) => {
  const a = s.indexOf(startMarker);
  const commit = 'await transaction.CommitAsync(cancellationToken);';
  const audit = s.lastIndexOf('await ', a);
  const c = s.indexOf(commit, a);
  const callEnd = c; // audit statement ends right before the commit line
  if (a < 0 || c < 0) throw new Error('no match: ' + label);
  const lineStart = s.lastIndexOf('\n', audit) + 1;
  const indent = s.slice(lineStart, audit);
  const auditText = s.slice(lineStart, s.lastIndexOf('\n', callEnd) + 1);
  const rest = s.slice(s.lastIndexOf('\n', callEnd) + 1);
  const commitLineEnd = rest.indexOf('\n') + 1;
  return s.slice(0, lineStart) + rest.slice(0, commitLineEnd) + auditText.replace(/connection, transaction, /, 'connection, transaction, ') + rest.slice(commitLineEnd);
}]);
rep(STORE, 'overload ignores the transaction', AUDIT_T, 'AppendInATransaction', 'return InsertAsync(connection, transaction, auditEvent, cancellationToken);', 'return InsertAsync(connection, null, auditEvent, cancellationToken);');
rep(STORE, 'batch accepts a null event', AUDIT_T, 'AppendBatchAsyncRejectsANullEvent', '            ArgumentNullException.ThrowIfNull(evt);\n', '');
rep(STORE, 'insert skips validation', AUDIT_T, '', '        evt.Validate();\n', '');
rep(IFACE, 'default overload silently succeeds', AUDIT_T, 'AStoreThatCannotJoin', '=> throw new NotSupportedException("This audit store cannot join a caller\'s transaction.");', '=> Task.CompletedTask;');
swapBeforeCommit(REF, 'rejection audit after commit', BILLING_T, 'WhoseAuditRecord', '"refund.rejected"');
swapBeforeCommit(BILL, 'cancellation audit after commit', BILLING_T, 'WhoseAuditRecord', 'eventName: "bill.cancelled"');

let survived = 0;
for (const [file, label, test, filter, fn] of mutants) {
  if (ONLY && !ONLY.includes(label)) continue;
  try {
    const original = nl(readFileSync(backups[file], 'utf8'));
    const crlf = readFileSync(backups[file], 'utf8').includes('\r\n');
    let applied = fn(original);
    if (crlf) applied = applied.replace(/\n/g, '\r\n');
    writeFileSync(file, applied);
    const args = ['test', test, '--nologo'].concat(filter ? ['--filter', `FullyQualifiedName~${filter}`] : []);
    let out = '';
    try {
      out = execFileSync(`${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`, args, { encoding: 'utf8', env: { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' }, stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 64 * 1024 * 1024 });
    } catch (e) { out = String(e.stdout || '') + String(e.stderr || ''); }
    const compileErr = /error CS/.test(out);
    const killed = /Başarısız!/.test(out) || compileErr;
    if (!killed) survived++;
    appendFileSync(LOG, `${killed ? (compileErr ? 'COMPILE ' : 'KILLED  ') : 'SURVIVED'} ${label}\n`);
  } catch (e) { appendFileSync(LOG, `ERROR    ${label}: ${e.message}\n`); }
  finally { copyFileSync(backups[file], file); }
}
for (const f of files) appendFileSync(LOG, `restore ${f.split('/').pop()}: ${Buffer.compare(readFileSync(f), readFileSync(backups[f])) === 0 ? 'identical' : 'DIFFERENT'}\n`);
appendFileSync(LOG, `mutants run: ${ONLY ? ONLY.length : mutants.length}, survived: ${survived}\n`);
