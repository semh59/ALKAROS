// Mutation run for V1-RMD-652: apply one mutant, run the anonymization tests, restore, verify identical.
import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const ROOT = 'D:/PROJECT/ALKAROS';
const STORE = `${ROOT}/src/Modules/CustomerData/AnonymizationState/PostgresCustomerAnonymizationRequestStore.cs`;
const LOG = `${ROOT}/evidence/V1-RMD-652/mutations.log`;
const TEST = `${ROOT}/tests/Modules/CustomerData/AnonymizationState/ALKAROS.CustomerData.AnonymizationState.Tests.csproj`;
const backup = join(tmpdir(), 'm652.orig');
copyFileSync(STORE, backup);
writeFileSync(LOG, '');
const nl = (s) => s.replace(/\r\n/g, '\n');

const CREATE = ['        await record(id, connection, transaction, cancellationToken);\n', '        await transaction.CommitAsync(cancellationToken);\n'];
const UPDATE = ['            await record(connection, transaction, cancellationToken);\n', '            await transaction.CommitAsync(cancellationToken);\n'];
const swap = ([record, commit]) => (s) => {
  const i = s.indexOf(record + commit);
  if (i < 0) throw new Error('no match');
  return s.slice(0, i) + commit + record + s.slice(i + record.length + commit.length);
};
const drop = ([record]) => (s) => {
  const i = s.indexOf(record);
  if (i < 0) throw new Error('no match');
  return s.slice(0, i) + s.slice(i + record.length);
};
const mutants = [
  ['request creation audit after commit', swap(CREATE)],
  ['status change audit after commit', swap(UPDATE)],
  ['request creation audit not recorded', drop(CREATE)],
  ['status change audit not recorded', drop(UPDATE)],
];

let survived = 0;
for (const [label, fn] of mutants) {
  try {
    const crlf = readFileSync(backup, 'utf8').includes('\r\n');
    let applied = fn(nl(readFileSync(backup, 'utf8')));
    if (crlf) applied = applied.replace(/\n/g, '\r\n');
    writeFileSync(STORE, applied);
    let out = '';
    try {
      out = execFileSync(`${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`, ['test', TEST, '--nologo'], { encoding: 'utf8', env: { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' }, stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 64 * 1024 * 1024 });
    } catch (e) { out = String(e.stdout || '') + String(e.stderr || ''); }
    const compileErr = /error CS/.test(out);
    const killed = /Başarısız!/.test(out) || compileErr;
    if (!killed) survived++;
    appendFileSync(LOG, `${killed ? (compileErr ? 'COMPILE ' : 'KILLED  ') : 'SURVIVED'} ${label}\n`);
  } catch (e) { appendFileSync(LOG, `ERROR    ${label}: ${e.message}\n`); }
  finally { copyFileSync(backup, STORE); }
}
appendFileSync(LOG, `restore ${STORE.split('/').pop()}: ${Buffer.compare(readFileSync(STORE), readFileSync(backup)) === 0 ? 'identical' : 'DIFFERENT'}\n`);
appendFileSync(LOG, `mutants: ${mutants.length}, survived: ${survived}\n`);
