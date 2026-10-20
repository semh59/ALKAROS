// Mutation run for V1-RMD-651: apply one mutant, run the retention tests, restore, verify identical.
import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const ROOT = 'D:/PROJECT/ALKAROS';
const STORE = `${ROOT}/src/Modules/Security/DataProtectionRetention/PostgresRetentionSubjectStore.cs`;
const LOG = `${ROOT}/evidence/V1-RMD-651/mutations.log`;
const TEST = `${ROOT}/tests/Modules/Security/DataProtectionRetention/ALKAROS.Security.DataProtectionRetention.Tests.csproj`;
const backup = join(tmpdir(), 'm651.orig');
copyFileSync(STORE, backup);
writeFileSync(LOG, '');
const nl = (s) => s.replace(/\r\n/g, '\n');

const RECORD = '        await record(connection, transaction, cancellationToken);\n';
const COMMIT = '        await transaction.CommitAsync(cancellationToken);\n';
const indexOfNth = (s, needle, n) => { let i = -1; for (let k = 0; k <= n; k++) { i = s.indexOf(needle, i + 1); if (i < 0) throw new Error('no match'); } return i; };
const mutants = [];
for (const [n, name] of [[0, 'disposal'], [1, 're-encryption'], [2, 'purge']]) {
  mutants.push([`${name} audit after commit`, (s) => {
    const i = indexOfNth(s, RECORD + COMMIT, n);
    return s.slice(0, i) + COMMIT + RECORD + s.slice(i + RECORD.length + COMMIT.length);
  }]);
}
mutants.push(['purge audit not recorded', (s) => {
  const i = indexOfNth(s, RECORD, 2);
  return s.slice(0, i) + s.slice(i + RECORD.length);
}]);

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
