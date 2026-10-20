// Mutation run for V1-RMD-656: apply one mutant, run the raw-insert guard test, restore, verify identical.
import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const ROOT = 'D:/PROJECT/ALKAROS';
const TESTFILE = `${ROOT}/tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs`;
const SRC = `${ROOT}/src/Host/Program.cs`;
const LOG = `${ROOT}/evidence/V1-RMD-656/mutations.log`;
const TEST = `${ROOT}/tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj`;
const files = [TESTFILE, SRC];
const backups = Object.fromEntries(files.map((f, i) => [f, join(tmpdir(), `m656-${i}.orig`)]));
for (const f of files) copyFileSync(f, backups[f]);
writeFileSync(LOG, '');
const nl = (s) => s.replace(/\r\n/g, '\n');

const mutants = [
  [TESTFILE, 'allow-list entry removed', (s) => {
    const entry = '        "src/Modules/OnlineOrdering/StoreStatus/OnlineStoreStatusService.cs",\n';
    const i = s.indexOf(entry); if (i < 0) throw new Error('no match');
    return s.slice(0, i) + s.slice(i + entry.length);
  }],
  [SRC, 'new raw audit insert in another file', (s) => s + '\n// INSERT INTO audit.audit_events (mutant)\n'],
  [TESTFILE, 'audit module not excluded', (s) => {
    const from = '!f.StartsWith(auditModule, StringComparison.Ordinal)\n                && ';
    const i = s.indexOf(from); if (i < 0) throw new Error('no match');
    return s.slice(0, i) + s.slice(i + from.length);
  }],
];

let survived = 0;
for (const [file, label, fn] of mutants) {
  try {
    const crlf = readFileSync(backups[file], 'utf8').includes('\r\n');
    let applied = fn(nl(readFileSync(backups[file], 'utf8')));
    if (crlf) applied = applied.replace(/\n/g, '\r\n');
    writeFileSync(file, applied);
    let out = '';
    try {
      out = execFileSync(`${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`, ['test', TEST, '--nologo', '--filter', 'FullyQualifiedName~RawAuditInserts'], { encoding: 'utf8', env: { ...process.env }, stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 64 * 1024 * 1024 });
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
