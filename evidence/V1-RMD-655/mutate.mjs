// Mutation run for V1-RMD-655: apply one mutant, run the audit tests, restore, verify identical.
import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const ROOT = 'D:/PROJECT/ALKAROS';
const FILE = `${ROOT}/src/Modules/Audit/EventStore/AuditEventWriter.cs`;
const LOG = `${ROOT}/evidence/V1-RMD-655/mutations.log`;
const TEST = `${ROOT}/tests/Modules/Audit/EventStore/ALKAROS.Audit.EventStore.Tests.csproj`;
const backup = join(tmpdir(), 'm655.orig');
copyFileSync(FILE, backup);
writeFileSync(LOG, '');
const nl = (s) => s.replace(/\r\n/g, '\n');

const rep = (label, from, to) => [label, (s) => { const i = s.indexOf(from); if (i < 0) throw new Error('no match: ' + label); return s.slice(0, i) + to + s.slice(i + from.length); }];
const mutants = [
  rep('validation skipped', '        evt.Validate();\n', ''),
  rep('metadata not sanitized', 'sanitizer.SanitizeJson(evt.MetadataJson)', 'evt.MetadataJson'),
  rep('before state not sanitized', 'sanitizer.SanitizeJson(evt.BeforeStateJson)', 'evt.BeforeStateJson'),
  rep('after state not sanitized', 'sanitizer.SanitizeJson(evt.AfterStateJson)', 'evt.AfterStateJson'),
];

let survived = 0;
for (const [label, fn] of mutants) {
  try {
    const crlf = readFileSync(backup, 'utf8').includes('\r\n');
    let applied = fn(nl(readFileSync(backup, 'utf8')));
    if (crlf) applied = applied.replace(/\n/g, '\r\n');
    writeFileSync(FILE, applied);
    let out = '';
    try {
      out = execFileSync(`${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`, ['test', TEST, '--nologo'], { encoding: 'utf8', env: { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' }, stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 64 * 1024 * 1024 });
    } catch (e) { out = String(e.stdout || '') + String(e.stderr || ''); }
    const compileErr = /error CS/.test(out);
    const killed = /Başarısız!/.test(out) || compileErr;
    if (!killed) survived++;
    appendFileSync(LOG, `${killed ? (compileErr ? 'COMPILE ' : 'KILLED  ') : 'SURVIVED'} ${label}\n`);
  } catch (e) { appendFileSync(LOG, `ERROR    ${label}: ${e.message}\n`); }
  finally { copyFileSync(backup, FILE); }
}
appendFileSync(LOG, `restore ${FILE.split('/').pop()}: ${Buffer.compare(readFileSync(FILE), readFileSync(backup)) === 0 ? 'identical' : 'DIFFERENT'}\n`);
appendFileSync(LOG, `mutants: ${mutants.length}, survived: ${survived}\n`);
