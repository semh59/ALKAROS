// Mutation run for V1-RMD-653: apply one mutant, run the budget tests, restore, verify identical.
import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const ROOT = 'D:/PROJECT/ALKAROS';
const FILE = `${ROOT}/src/Host/Observability/PerformanceBudgetEndpoints/PerformanceBudgetEndpoints.cs`;
const LOG = `${ROOT}/evidence/V1-RMD-653/mutations.log`;
const TEST = `${ROOT}/tests/Host/Experience/Observability/ALKAROS.Host.Experience.Observability.Tests.csproj`;
const backup = join(tmpdir(), 'm653.orig');
copyFileSync(FILE, backup);
writeFileSync(LOG, '');
const nl = (s) => s.replace(/\r\n/g, '\n');

const rep = (label, from, to) => [label, (s) => { const i = s.indexOf(from); if (i < 0) throw new Error('no match: ' + label); return s.slice(0, i) + to + s.slice(i + from.length); }];
const SET_AUDIT = '            await AuditAsync(audit, "monitoring.performance_budget.set", actorId, item, cancellationToken);\n';
const SET_DO = '            await queries.SetAsync(item, cancellationToken);\n';
const mutants = [
  rep('set: change before audit', SET_AUDIT + SET_DO, SET_DO + SET_AUDIT),
  rep('remove: removed without existence check first', '            if (existing is null)\n                return Results.NotFound();\n', ''),
  rep('remove: change before audit',
    '            await AuditAsync(audit, "monitoring.performance_budget.removed", actorId, new PerformanceBudgetItem(metric, name, 0), cancellationToken);\n            return await queries.RemoveAsync(metric, name, cancellationToken) ? Results.NoContent() : Results.NotFound();',
    '            var removedNow = await queries.RemoveAsync(metric, name, cancellationToken);\n            await AuditAsync(audit, "monitoring.performance_budget.removed", actorId, new PerformanceBudgetItem(metric, name, 0), cancellationToken);\n            return removedNow ? Results.NoContent() : Results.NotFound();'),
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
      out = execFileSync(`${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`, ['test', TEST, '--nologo', '--filter', 'FullyQualifiedName~PerformanceBudgetHttp'], { encoding: 'utf8', env: { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' }, stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 64 * 1024 * 1024 });
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
