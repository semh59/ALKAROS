import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
const R = 'D:/PROJECT/ALKAROS';
const UC = `${R}/src/BuildingBlocks/Measurements/UnitConverter.cs`;
const LD = `${R}/src/Host/Experience/Recipes/UnitConversionLoaderHostedService.cs`;
const LOG = `${R}/evidence/V1-RMD-660/mutations.log`;
const M = `${R}/tests/BuildingBlocks/Measurements/ALKAROS.Measurements.Tests.csproj`;
const H = `${R}/tests/Host/Experience/Recipes/ALKAROS.Host.Experience.Recipes.Tests.csproj`;
const files = [UC, LD];
const bak = Object.fromEntries(files.map((f, i) => [f, join(tmpdir(), `m660-${i}.orig`)]));
for (const f of files) copyFileSync(f, bak[f]);
if (!process.env.ONLY) writeFileSync(LOG, '');
const sub = (file, label, from, to, test, filter) => ({ file, label, test, filter, fn: (s) => { const i = s.indexOf(from); if (i < 0) throw new Error('no match ' + label); return s.slice(0, i) + to + s.slice(i + from.length); } });
const mutants = [
  sub(UC, 'TryConvert ignores custom rule first', 'if (TryApplyCustom(from, to, quantity, out result))\n            return true;', '', M),
  sub(UC, 'Convert ignores custom rule first', 'if (from != to && TryApplyCustom(from, to, quantity, out var registered))\n            return registered;', '', M),
  sub(UC, 'reverse multiplies instead of divides', 'result = quantity / reverseFactor;', 'result = quantity * (1.0m / reverseFactor);', M),
  sub(UC, 'direct rule disabled', 'if (_directlyRegisteredPairs.ContainsKey((from, to)) && _customConversions.TryGetValue((from, to), out var factor))', 'if (false && _customConversions.TryGetValue((from, to), out var factor))', M),
  sub(UC, 'physical contradiction accepted', 'if (Math.Abs(factor - physicalFactor) > InvertibilityTolerance)', 'if (false)', M),
  sub(UC, 'Count units treated as physical', 'knownFrom.Dimension != UnitDimension.Count)', 'true)', M),
  sub(LD, 'no background retry', '_retry = RetryUntilLoadedAsync(_stopping.Token);', '_retry = Task.CompletedTask;', H, 'UnitConversionLoaderRetry'),
  sub(LD, 'retry never stops', 'await _stopping.CancelAsync().ConfigureAwait(false);', '', H, 'UnitConversionLoaderRetry'),
];
let survived = 0;
for (const m of mutants) {
  if (process.env.ONLY && !m.label.includes(process.env.ONLY)) continue;
  try {
    const orig = readFileSync(bak[m.file], 'utf8'); const crlf = orig.includes('\r\n');
    let a = m.fn(orig.replace(/\r\n/g, '\n')); if (crlf) a = a.replace(/\n/g, '\r\n');
    writeFileSync(m.file, a);
    let out = '';
    const args = ['test', m.test, '--nologo']; if (m.filter) args.push('--filter', m.filter);
    try { out = execFileSync(`${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`, args, { encoding: 'utf8', env: { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' }, maxBuffer: 1 << 28, stdio: ['ignore', 'pipe', 'pipe'] }); }
    catch (e) { out = String(e.stdout || '') + String(e.stderr || ''); }
    const compile = /error CS/.test(out); const killed = /Başarısız!/.test(out) || compile;
    if (!killed) survived++;
    appendFileSync(LOG, `${killed ? (compile ? 'COMPILE ' : 'KILLED  ') : 'SURVIVED'} ${m.label}\n`);
  } catch (e) { appendFileSync(LOG, `ERROR    ${m.label}: ${e.message}\n`); }
  finally { copyFileSync(bak[m.file], m.file); }
}
for (const f of files) appendFileSync(LOG, `restore ${f.split('/').pop()}: ${Buffer.compare(readFileSync(f), readFileSync(bak[f])) === 0 ? 'identical' : 'DIFFERENT'}\n`);
appendFileSync(LOG, `mutants: ${mutants.length}, survived: ${survived}\n`);
