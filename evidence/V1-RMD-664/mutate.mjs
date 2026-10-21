import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
const R = 'D:/PROJECT/ALKAROS';
const S = `${R}/src/BuildingBlocks`;
const AES = `${S}/Security/SensitiveData/AesGcmEnvelopeCipher.cs`;
const ENV = `${S}/Security/SensitiveData/SensitiveEnvelope.cs`;
const KEY = `${S}/Security/SensitiveData/EnvelopeKeyCodec.cs`;
const OEV = `${S}/Messaging/OutboxEnvelope.cs`;
const OST = `${S}/Messaging/OutboxStore.cs`;
const RPO = `${S}/Messaging/RetryPolicy.cs`;
const ARC = `${R}/tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs`;
const SD = `${R}/tests/BuildingBlocks/Security/SensitiveData/ALKAROS.SensitiveData.Tests.csproj`;
const MS = `${R}/tests/BuildingBlocks/Messaging/ALKAROS.Messaging.Tests.csproj`;
const AR = `${R}/tests/Architecture/ModuleBoundaries`;
const LOG = `${R}/evidence/V1-RMD-664/mutations.log`;
const DOTNET = `${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`;
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
const files = [AES, ENV, KEY];
const bak = Object.fromEntries(files.map((f, i) => [f, join(tmpdir(), `m664-${i}.orig`)]));
for (const f of files) copyFileSync(f, bak[f]);
if (!process.env.ONLY) writeFileSync(LOG, '');
const sub = (file, label, from, to, test) => ({ file, label, test, fn: (s) => { const i = s.indexOf(from); if (i < 0) throw new Error('no match ' + label); return s.slice(0, i) + to + s.slice(i + from.length); } });
const mutants = [
  sub(AES, 'wrong-size nonce/tag not converted', 'when (exception is CryptographicException or ArgumentException)', 'when (exception is CryptographicException)', SD),
  sub(ENV, 'missing envelope field not converted', 'when (exception is System.Text.Json.JsonException or ArgumentException)', 'when (exception is System.Text.Json.JsonException)', SD),
  sub(AES, 'fixed nonce', 'RandomNumberGenerator.GetBytes(NonceLengthBytes)', 'new byte[NonceLengthBytes]', SD),
  sub(KEY, 'short keys accepted', 'if (key.Length != KeyLengthBytes)', 'if (key.Length < 16)', SD),
];
let survived = 0;
for (const m of mutants) {
  if (process.env.ONLY && !m.label.includes(process.env.ONLY)) continue;
  try {
    const orig = readFileSync(bak[m.file], 'utf8'); const crlf = orig.includes('\r\n');
    let a = m.fn(orig.replace(/\r\n/g, '\n')); if (crlf) a = a.replace(/\n/g, '\r\n');
    writeFileSync(m.file, a);
    let out = '';
    try { out = execFileSync(DOTNET, ['test', m.test, '--nologo'], { encoding: 'utf8', timeout: 300000, env, maxBuffer: 1 << 28, stdio: ['ignore', 'pipe', 'pipe'] }); }
    catch (e) { out = String(e.stdout || '') + String(e.stderr || '') + (e.code === 'ETIMEDOUT' ? ' Başarısız! (timeout)' : ''); }
    const compile = /error (CS|CA)/.test(out); const killed = /Başarısız!/.test(out) || compile;
    if (!killed && !/Başarılı!/.test(out)) { appendFileSync(LOG, `INFRA    ${m.label}\n`); continue; }
    if (!killed) survived++;
    appendFileSync(LOG, `${killed ? (compile ? 'COMPILE ' : 'KILLED  ') : 'SURVIVED'} ${m.label}\n`);
  } catch (e) { appendFileSync(LOG, `ERROR    ${m.label}: ${e.message}\n`); }
  finally {
    copyFileSync(bak[m.file], m.file);
    try { execFileSync('taskkill', ['/F', '/IM', 'testhost.exe'], { stdio: 'ignore' }); } catch { /* none running */ }
  }
}
for (const f of files) appendFileSync(LOG, `restore ${f.split('/').pop()}: ${Buffer.compare(readFileSync(f), readFileSync(bak[f])) === 0 ? 'identical' : 'DIFFERENT'}\n`);
appendFileSync(LOG, `mutants: ${mutants.length}, survived: ${survived}\n`);
