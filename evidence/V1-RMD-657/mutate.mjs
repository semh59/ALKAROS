// Mutation run for V1-RMD-657: apply one mutant, run the audit tests, restore, verify identical.
import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const ROOT = 'D:/PROJECT/ALKAROS';
const STORE = `${ROOT}/src/Modules/Audit/EventStore/PostgresAuditEventStore.cs`;
const MODULE = `${ROOT}/src/Modules/Audit/AuditModule.cs`;
const LOG = `${ROOT}/evidence/V1-RMD-657/mutations.log`;
const TEST = `${ROOT}/tests/Modules/Audit/EventStore/ALKAROS.Audit.EventStore.Tests.csproj`;
const files = [STORE, MODULE];
const backups = Object.fromEntries(files.map((f, i) => [f, join(tmpdir(), `m657-${i}.orig`)]));
for (const f of files) copyFileSync(f, backups[f]);
writeFileSync(LOG, '');
const nl = (s) => s.replace(/\r\n/g, '\n');

const all = (file, label, from, to) => [file, label, (s) => { if (!s.includes(from)) throw new Error('no match: ' + label); return s.split(from).join(to); }];
const first = (file, label, from, to) => [file, label, (s) => { const i = s.indexOf(from); if (i < 0) throw new Error('no match: ' + label); return s.slice(0, i) + to + s.slice(i + from.length); }];
const mutants = [
  all(STORE, 'read order without the id tie-break', 'ORDER BY occurred_at ASC, id ASC', 'ORDER BY occurred_at ASC'),
  all(STORE, 'read order descending', 'ORDER BY occurred_at ASC, id ASC', 'ORDER BY occurred_at DESC, id ASC'),
  all(STORE, 'cap read without the extra row', 'LIMIT {MaxUnpagedRows + 1}', 'LIMIT {MaxUnpagedRows}'),
  first(STORE, 'aggregate cap check is off by one', 'if (list.Count > MaxUnpagedRows)', 'if (list.Count >= MaxUnpagedRows)'),
  all(STORE, 'both cap checks off by one', 'if (list.Count > MaxUnpagedRows)', 'if (list.Count >= MaxUnpagedRows)'),
  [STORE, 'correlation cap check off by one', (s) => { const a = s.indexOf('if (list.Count > MaxUnpagedRows)'); const b = s.indexOf('if (list.Count > MaxUnpagedRows)', a + 1); if (b < 0) throw new Error('second check'); return s.slice(0, b) + 'if (list.Count >= MaxUnpagedRows)' + s.slice(b + 'if (list.Count > MaxUnpagedRows)'.length); }],
  first(STORE, 'aggregate type filter dropped', 'WHERE aggregate_type = @aggregate_type AND aggregate_id = @aggregate_id', 'WHERE aggregate_id = @aggregate_id'),
  all(MODULE, 'store registered transient', 'RegisterSingleton<IAuditEventStore, PostgresAuditEventStore>', 'RegisterTransient<IAuditEventStore, PostgresAuditEventStore>'),
  all(MODULE, 'sanitizer registered transient', 'RegisterSingleton<IAuditSanitizer, AuditSanitizer>', 'RegisterTransient<IAuditSanitizer, AuditSanitizer>'),
  first(STORE, 'batch not all-or-nothing', 'await tx.CommitAsync(cancellationToken).ConfigureAwait(false);\n    }', 'await tx.CommitAsync(cancellationToken).ConfigureAwait(false);\n    }'),
];
mutants.pop(); // the batch transaction is exercised by the atomic test; an inert string swap is not a mutant

let survived = 0;
for (const [file, label, fn] of mutants) {
  try {
    const crlf = readFileSync(backups[file], 'utf8').includes('\r\n');
    let applied = fn(nl(readFileSync(backups[file], 'utf8')));
    if (crlf) applied = applied.replace(/\n/g, '\r\n');
    writeFileSync(file, applied);
    let out = '';
    try {
      out = execFileSync(`${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`, ['test', TEST, '--nologo'], { encoding: 'utf8', env: { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' }, stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 64 * 1024 * 1024 });
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
