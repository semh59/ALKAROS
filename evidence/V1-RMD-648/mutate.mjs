// Mutation run for V1-RMD-648: apply one mutant, run the billing tests, restore, verify identical.
import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const ROOT = 'D:/PROJECT/ALKAROS';
const STORE = `${ROOT}/src/Host/Experience/Orders/CashierHandoffStore.cs`;
const LOG = `${ROOT}/evidence/V1-RMD-648/mutations.log`;
const TEST = `${ROOT}/tests/Host/Experience/Orders/TableDraft/ALKAROS.Host.Experience.Orders.TableDraft.Tests.csproj`;
const files = [STORE];
const backups = Object.fromEntries(files.map((f, i) => [f, join(tmpdir(), `m648-${i}.orig`)]));
for (const f of files) copyFileSync(f, backups[f]);
writeFileSync(LOG, '');
const nl = (s) => s.replace(/\r\n/g, '\n');

const mutants = [];
const rep = (label, from, to) => mutants.push([STORE, label, (s) => { if (!s.includes(from)) throw new Error('no match: ' + label); return s.replace(from, to); }]);
const swapAfterCommit = (label, marker) => mutants.push([STORE, label, (s) => {
  const a = s.indexOf(marker);
  const audit = s.lastIndexOf('await ', a);
  const commit = 'await transaction.CommitAsync(cancellationToken);';
  const c = s.indexOf(commit, a);
  if (a < 0 || c < 0) throw new Error('no match: ' + label);
  const auditStart = s.lastIndexOf('\n', audit) + 1;
  const commitStart = s.lastIndexOf('\n', c) + 1;
  const commitEnd = s.indexOf('\n', c) + 1;
  return s.slice(0, auditStart) + s.slice(commitStart, commitEnd) + s.slice(auditStart, commitStart) + s.slice(commitEnd);
}]);
swapAfterCommit('recall audit after commit', 'eventName: "check.recalled-from-cashier"');
rep('recall audit event name changed', 'eventName: "check.recalled-from-cashier"', 'eventName: "check.recalled"');

let survived = 0;
for (const [file, label, fn] of mutants) {
  try {
    const crlf = readFileSync(backups[file], 'utf8').includes('\r\n');
    let applied = fn(nl(readFileSync(backups[file], 'utf8')));
    if (crlf) applied = applied.replace(/\n/g, '\r\n');
    writeFileSync(file, applied);
    let out = '';
    try {
      out = execFileSync(`${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`, ['test', TEST, '--nologo', '--filter', 'FullyQualifiedName~ACheckSentByMistake|FullyQualifiedName~ARecallWhoseAudit'], { encoding: 'utf8', env: { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' }, stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 64 * 1024 * 1024 });
    } catch (e) { out = String(e.stdout || '') + String(e.stderr || ''); }
    const compileErr = /error CS/.test(out);
    const killed = /Başarısız!/.test(out) || compileErr;
    if (!killed) survived++;
    appendFileSync(LOG, `${killed ? (compileErr ? 'COMPILE ' : 'KILLED  ') : 'SURVIVED'} ${label}\n`);
  } catch (e) { appendFileSync(LOG, `ERROR    ${label}: ${e.message}\n`); }
  finally { copyFileSync(backups[file], file); }
}
for (const f of files) appendFileSync(LOG, `restore ${f.split('/').pop()}: ${Buffer.compare(readFileSync(f), readFileSync(backups[f])) === 0 ? 'identical' : 'DIFFERENT'}\n`);
appendFileSync(LOG, `mutants: ${mutants.length}, survived: ${survived}\n`);
