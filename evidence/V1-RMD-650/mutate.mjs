// Mutation run for V1-RMD-650: apply one mutant, run the security administration tests, restore, verify identical.
import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const ROOT = 'D:/PROJECT/ALKAROS';
const D = `${ROOT}/src/Host/Experience/SecurityAdministration/`;
const OUTBOX = `${D}OutboxAdministration.cs`;
const SECRET = `${D}SecretRotationAdministration.cs`;
const BACKLOG = `${D}OrderBacklogAdministration.cs`;
const LOG = `${ROOT}/evidence/V1-RMD-650/mutations.log`;
const TEST = `${ROOT}/tests/Host/Experience/SecurityAdministration/ALKAROS.Host.Experience.SecurityAdministration.Tests.csproj`;
const files = [OUTBOX, SECRET, BACKLOG];
const backups = Object.fromEntries(files.map((f, i) => [f, join(tmpdir(), `m650-${i}.orig`)]));
for (const f of files) copyFileSync(f, backups[f]);
writeFileSync(LOG, '');
const nl = (s) => s.replace(/\r\n/g, '\n');

const mutants = [];
const rep = (file, label, from, to) => mutants.push([file, label, (s) => { if (!s.includes(from)) throw new Error('no match: ' + label); return s.split(from).join(to); }]);
// the audit record is written on its own connection instead of the update transaction
mutants.push([OUTBOX, 'requeue audit not in the update transaction', (s) => {
  if (!s.includes('connection, transaction, cancellationToken);\n                await transaction.CommitAsync')) throw new Error('no match: outbox');
  return s.replace('await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))', 'await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))')
    .replace('                    connection, transaction, cancellationToken);\n                await transaction.CommitAsync(cancellationToken);', '                    cancellationToken);\n                await transaction.CommitAsync(cancellationToken);');
}]);
for (const [op, save, args] of [
  ['initialize', 'store.Save(record);', '"initialize", name, 1, cancellationToken);'],
  ['rotate', 'store.Save(rotated);', '"rotate", name, nextVersion, cancellationToken);'],
  ['revoke', 'store.Save(revoked);', '"revoke", name, request.Version, cancellationToken);'],
  ['rollback', 'store.Save(rolledBack);', '"rollback", name, request.Version, cancellationToken);']]) {
  const audit = `await AuditAsync(audit, context, ${args}`;
  rep(SECRET, `${op} saved before audit`, `            ${audit}\n            ${save}`, `            ${save}\n            ${audit}`);
}
rep(BACKLOG, 'backfill intent event name changed', 'eventName: "orders.backfill.started"', 'eventName: "orders.backfill.begun"');

let survived = 0;
for (const [file, label, fn] of mutants) {
  try {
    const crlf = readFileSync(backups[file], 'utf8').includes('\r\n');
    let applied = fn(nl(readFileSync(backups[file], 'utf8')));
    if (crlf) applied = applied.replace(/\n/g, '\r\n');
    writeFileSync(file, applied);
    let out = '';
    try {
      out = execFileSync(`${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`, ['test', TEST, '--nologo', '--filter', 'FullyQualifiedName~LeavesNoTraceOfTheAction'], { encoding: 'utf8', env: { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' }, stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 64 * 1024 * 1024 });
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
