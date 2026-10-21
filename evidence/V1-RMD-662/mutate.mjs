import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
const R = 'D:/PROJECT/ALKAROS';
const SD = `${R}/src/Host/Observability/SecurityDetectors/SecurityDetectors.cs`;
const CY = `${R}/compose.yaml`;
const LOG = `${R}/evidence/V1-RMD-662/mutations.log`;
const DOTNET = `${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`;
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
const files = [SD, CY];
const bak = Object.fromEntries(files.map((f, i) => [f, join(tmpdir(), `m662-${i}.orig`)]));
for (const f of files) copyFileSync(f, bak[f]);
writeFileSync(LOG, '');
const dotnetTest = () => ['dotnet', DOTNET, ['test', `${R}/tests/Host/Observability/SecurityDetectors/ALKAROS.Host.Observability.SecurityDetectors.Tests.csproj`, '--nologo']];
const pyTest = () => ['python', 'python', ['-m', 'pytest', `${R}/tests/Deployment`, '-q', '-p', 'no:cacheprovider']];
const sub = (file, label, from, to, run) => ({ file, label, run, fn: (s) => { const i = s.indexOf(from); if (i < 0) throw new Error('no match ' + label); return s.slice(0, i) + to + s.slice(i + from.length); } });
const mutants = [
  sub(SD, 'first check ignores existing dead letters', 'var rose = dead > Math.Max(_lastDead, 0);', 'var rose = _lastDead >= 0 && dead > _lastDead;', dotnetTest),
  sub(CY, 'connector without master key', '    environment:\n      # The tunnel token', '    xenvironment:\n      # The tunnel token', pyTest),
  sub(CY, 'connector key renamed', 'ALKAROS_SECRET_ENVELOPE_MASTER_KEY: ${ALKAROS_SECRET_ENVELOPE_MASTER_KEY:?Set ALKAROS_SECRET_ENVELOPE_MASTER_KEY in a local .env file (openssl rand -base64 32)}\n    depends_on:\n      api: { condition: service_healthy }\n    # V12-QRT-005', 'ALKAROS_SECRET_ENVELOPE_MASTER_KEYX: ${ALKAROS_SECRET_ENVELOPE_MASTER_KEY:?Set}\n    depends_on:\n      api: { condition: service_healthy }\n    # V12-QRT-005', pyTest),
];
let survived = 0;
for (const m of mutants) {
  try {
    const orig = readFileSync(bak[m.file], 'utf8'); const crlf = orig.includes('\r\n');
    let a = m.fn(orig.replace(/\r\n/g, '\n')); if (crlf) a = a.replace(/\n/g, '\r\n');
    writeFileSync(m.file, a);
    const [kind, exe, args] = m.run();
    let out = ''; let failed = false;
    try { out = execFileSync(exe, args, { encoding: 'utf8', timeout: 300000, env, maxBuffer: 1 << 28, stdio: ['ignore', 'pipe', 'pipe'] }); }
    catch (e) { out = String(e.stdout || '') + String(e.stderr || ''); failed = true; }
    const killed = kind === 'dotnet' ? /Başarısız!/.test(out) || /error (CS|CA)/.test(out) : /\d+ failed/.test(out);
    const passed = kind === 'dotnet' ? /Başarılı!/.test(out) : /\d+ passed/.test(out) && !failed;
    if (!killed && !passed) { appendFileSync(LOG, `INFRA    ${m.label}\n`); continue; }
    if (!killed) survived++;
    appendFileSync(LOG, `${killed ? 'KILLED  ' : 'SURVIVED'} ${m.label}\n`);
  } catch (e) { appendFileSync(LOG, `ERROR    ${m.label}: ${e.message}\n`); }
  finally {
    copyFileSync(bak[m.file], m.file);
    try { execFileSync('taskkill', ['/F', '/IM', 'testhost.exe'], { stdio: 'ignore' }); } catch { /* none running */ }
  }
}
for (const f of files) appendFileSync(LOG, `restore ${f.split('/').pop()}: ${Buffer.compare(readFileSync(f), readFileSync(bak[f])) === 0 ? 'identical' : 'DIFFERENT'}\n`);
appendFileSync(LOG, `mutants: ${mutants.length}, survived: ${survived}\n`);
