// Mutation run for V1-RMD-649: apply one mutant, run the PaymentTender tests, restore, verify identical.
import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const ROOT = 'D:/PROJECT/ALKAROS';
const M = `${ROOT}/src/Modules/Payments/ManualResolution/`;
const SERVICE = `${M}ManualPaymentResolutionService.cs`;
const CARD = `${M}ManualCardConfirmationService.cs`;
const LOG = `${ROOT}/evidence/V1-RMD-649/mutations.log`;
const TEST = `${ROOT}/tests/Host/Experience/PaymentTender/ALKAROS.Host.Experience.PaymentTender.Tests.csproj`;
const files = [SERVICE, CARD];
const backups = Object.fromEntries(files.map((f, i) => [f, join(tmpdir(), `m649-${i}.orig`)]));
for (const f of files) copyFileSync(f, backups[f]);
writeFileSync(LOG, '');
const nl = (s) => s.replace(/\r\n/g, '\n');

const mutants = [];
// Move the recorder call after the commit: the audit is no longer part of the transaction.
const afterCommit = (file, label, recordCall, commitCall) => mutants.push([file, label, (s) => {
  const r = s.indexOf(recordCall);
  if (r < 0) throw new Error('no match: ' + label);
  const rEnd = s.indexOf('\n', r) + 1;
  const c = s.indexOf(commitCall, rEnd);
  const cEnd = s.indexOf('\n', c) + 1;
  return s.slice(0, r) + s.slice(rEnd, cEnd) + s.slice(r, rEnd) + s.slice(cEnd);
}]);
afterCommit(SERVICE, 'not-charged audit after commit', 'await record(result, connection, transaction, cancellationToken);', 'await transaction.CommitAsync(cancellationToken);');
afterCommit(CARD, 'claim audit after commit', 'await record(confirmation, connection, transaction, cancellationToken);', 'await transaction.CommitAsync(cancellationToken);');
afterCommit(CARD, 'approval audit after commit', 'await record(approvedConfirmation, connection, transaction, cancellationToken);', 'await transaction.CommitAsync(cancellationToken);');
afterCommit(CARD, 'rejection audit after commit', 'await record(rejectedConfirmation, connection, transaction, cancellationToken);', 'await transaction.CommitAsync(cancellationToken);');

let survived = 0;
for (const [file, label, fn] of mutants) {
  try {
    const crlf = readFileSync(backups[file], 'utf8').includes('\r\n');
    let applied = fn(nl(readFileSync(backups[file], 'utf8')));
    if (crlf) applied = applied.replace(/\n/g, '\r\n');
    writeFileSync(file, applied);
    let out = '';
    try {
      out = execFileSync(`${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`, ['test', TEST, '--nologo', '--filter', 'FullyQualifiedName~WhoseAuditRecord'], { encoding: 'utf8', env: { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' }, stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 64 * 1024 * 1024 });
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
