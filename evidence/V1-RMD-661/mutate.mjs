import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
const R = 'D:/PROJECT/ALKAROS';
const OS = `${R}/src/BuildingBlocks/Messaging/OutboxStore.cs`;
const RP = `${R}/src/BuildingBlocks/Messaging/RetryPolicy.cs`;
const LOG = `${R}/evidence/V1-RMD-661/mutations.log`;
const M = `${R}/tests/BuildingBlocks/Messaging/ALKAROS.Messaging.Tests.csproj`;
const files = [OS, RP];
const bak = Object.fromEntries(files.map((f, i) => [f, join(tmpdir(), `m661-${i}.orig`)]));
for (const f of files) copyFileSync(f, bak[f]);
if (!process.env.ONLY) writeFileSync(LOG, '');
const sub = (file, label, from, to) => ({ file, label, fn: (s) => { const i = s.indexOf(from); if (i < 0) throw new Error('no match ' + label); return s.slice(0, i) + to + s.slice(i + from.length); } });
const mutants = [
  sub(OS, 'timeout not a failure (old catch filter)', 'catch (Exception ex) when (!cancellationToken.IsCancellationRequested)', 'catch (Exception ex) when (ex is not OperationCanceledException)'),
  sub(OS, 'no delivery timeout', 'deliveryScope.CancelAfter(_deliveryTimeout);', ''),
  sub(OS, 'expired lease does not count', 'attempt_count = attempt_count + 1, last_error = \'lease expired\'', 'last_error = \'lease expired\''),
  sub(OS, 'expired lease never dies', 'reader.GetInt32(2) + 1 >= maxAttempts', 'false'),
  sub(OS, 'finalize uses shutdown token', 'message.Id, leaseGeneration, CancellationToken.None).ConfigureAwait(false);\n        else', 'message.Id, leaseGeneration, cancellationToken).ConfigureAwait(false);\n        else'),
  sub(OS, 'rest of batch not released', 'await ReleaseAsync(messages.Skip(index).ToList()).ConfigureAwait(false);', ''),
  sub(OS, 'lease not renewed', 'SET claimed_at = now() WHERE id = $1', 'SET claimed_at = claimed_at WHERE id = $1'),
  sub(OS, 'lost lease not skipped', 'ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;', 'ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) >= 0;'),
  sub(OS, 'delivered retention too long', 'DispatchedRetention = TimeSpan.FromDays(14)', 'DispatchedRetention = TimeSpan.FromDays(16)'),
  sub(OS, 'dead retention too short', 'DeadRetention = TimeSpan.FromDays(90)', 'DeadRetention = TimeSpan.FromDays(88)'),
  sub(OS, 'purge deletes pending', "OR (status = 'dead' AND", "OR (status <> 'dispatched' AND"),
  sub(OS, 'pacing never grows', 'Idle.Ticks << doublings', 'Idle.Ticks'),
  sub(OS, 'pacing cap too high', 'MaxFaultDelay = TimeSpan.FromSeconds(30)', 'MaxFaultDelay = TimeSpan.FromSeconds(60)'),
  sub(RP, 'error type name dropped', 'return TypeNameShape.IsMatch(error) ? $"handler failure: {error}" : "handler failure";', 'return "handler failure";'),
];
let survived = 0;
for (const m of mutants) {
  if (process.env.ONLY && !m.label.includes(process.env.ONLY)) continue;
  try {
    const orig = readFileSync(bak[m.file], 'utf8'); const crlf = orig.includes('\r\n');
    let a = m.fn(orig.replace(/\r\n/g, '\n')); if (crlf) a = a.replace(/\n/g, '\r\n');
    writeFileSync(m.file, a);
    let out = '';
    try { out = execFileSync(`${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`, ['test', M, '--nologo'], { encoding: 'utf8', timeout: 300000, env: { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' }, maxBuffer: 1 << 28, stdio: ['ignore', 'pipe', 'pipe'] }); }
    catch (e) { out = String(e.stdout || '') + String(e.stderr || '') + (e.code === 'ETIMEDOUT' ? ' Başarısız! (timeout)' : ''); }
    writeFileSync(`${R}/evidence/V1-RMD-661/last.out`, out);
    const compile = /error (CS|CA)/.test(out); const infra = !compile && !/Başarısız!/.test(out) && !/Başarılı!/.test(out); const killed = /Başarısız!/.test(out) || compile; if (infra) { appendFileSync(LOG, `INFRA    ${m.label}
`); continue; }
    if (!killed) survived++;
    appendFileSync(LOG, `${killed ? (compile ? 'COMPILE ' : 'KILLED  ') : 'SURVIVED'} ${m.label}\n`);
  } catch (e) { appendFileSync(LOG, `ERROR    ${m.label}: ${e.message}\n`); }
  finally {
    copyFileSync(bak[m.file], m.file);
    // A mutant that hangs a test leaves its testhost holding the dll, which would break every later build.
    try { execFileSync('taskkill', ['/F', '/IM', 'testhost.exe'], { stdio: 'ignore' }); } catch { /* none running */ }
  }
}
for (const f of files) appendFileSync(LOG, `restore ${f.split('/').pop()}: ${Buffer.compare(readFileSync(f), readFileSync(bak[f])) === 0 ? 'identical' : 'DIFFERENT'}\n`);
appendFileSync(LOG, `mutants: ${mutants.length}, survived: ${survived}\n`);
