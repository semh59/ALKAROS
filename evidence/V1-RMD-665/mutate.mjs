import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
const R = 'D:/PROJECT/ALKAROS';
const STORE = `${R}/src/Host/Experience/KitchenOperations/KitchenOperationsStore.cs`;
const PUB = `${R}/src/Modules/Kitchen/OrderItemStateSync/KitchenOrderItemStateSyncPublisher.cs`;
const HT = `${R}/tests/Host/Experience/KitchenOperations/ALKAROS.Host.Experience.KitchenOperations.Tests.csproj`;
const MT = `${R}/tests/Modules/Kitchen/OrderItemStateSync/ALKAROS.Kitchen.OrderItemStateSync.Tests.csproj`;
const LOG = `${R}/evidence/V1-RMD-665/mutations.log`;
const DOTNET = `${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`;
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
const files = [STORE, PUB];
const bak = Object.fromEntries(files.map((f, i) => [f, join(tmpdir(), `m665-${i}.orig`)]));
for (const f of files) copyFileSync(f, bak[f]);
if (!process.env.ONLY) writeFileSync(LOG, '');
const sub = (file, label, from, to, test, extra) => ({ file, label, test, extra, fn: (s) => { const i = s.indexOf(from); if (i < 0) throw new Error('no match ' + label); return s.slice(0, i) + to + s.slice(i + from.length); } });
const FILTER = ['--filter', 'RolledBackWhenItsStateEvent'];
const mutants = [
  sub(STORE, 'ticket saved outside the transaction', 'await _tickets.SaveAsync(ticket, expectedRowVersion, connection, transaction, cancellationToken);', 'await _tickets.SaveAsync(ticket, expectedRowVersion, cancellationToken);', HT, FILTER),
  sub(STORE, 'event never written', 'await KitchenOrderItemStateSyncPublisher.PublishAsync(\n            ticket.OrderId, item, liveSyncEnabled, connection, transaction, cancellationToken);', '', HT, FILTER),
  sub(STORE, 'transaction never committed', 'await transaction.CommitAsync(cancellationToken);\n        return liveSyncEnabled;', 'return liveSyncEnabled;', HT, FILTER),
  sub(PUB, 'disabled still enqueues', 'if (!enabled)', 'if (enabled)', MT, []),
  sub(PUB, 'event not enqueued', 'await OutboxStore.EnqueueAsync(envelope, connection, transaction, cancellationToken);', 'await Task.CompletedTask;', MT, []),
  sub(PUB, 'empty order id accepted', 'if (orderId == Guid.Empty)', 'if (false)', MT, []),
];
let survived = 0;
for (const m of mutants) {
  if (process.env.ONLY && !m.label.includes(process.env.ONLY)) continue;
  try {
    const orig = readFileSync(bak[m.file], 'utf8'); const crlf = orig.includes('\r\n');
    let a = m.fn(orig.replace(/\r\n/g, '\n')); if (crlf) a = a.replace(/\n/g, '\r\n');
    writeFileSync(m.file, a);
    let out = '';
    try { out = execFileSync(DOTNET, ['test', m.test, '--nologo', ...m.extra], { encoding: 'utf8', timeout: 300000, env, maxBuffer: 1 << 28, stdio: ['ignore', 'pipe', 'pipe'] }); }
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
