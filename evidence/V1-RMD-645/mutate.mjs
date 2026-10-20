// Mutation run for V1-RMD-645: apply one mutant, run the audit disposal tests, restore, verify identical.
import { execFileSync } from 'node:child_process';
import { copyFileSync, readFileSync, writeFileSync, appendFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const ROOT = 'D:/PROJECT/ALKAROS';
const DISP = `${ROOT}/src/Modules/Audit/PartitionDisposal/AuditPartitionDisposal.cs`;
const PROG = `${ROOT}/src/Host/Program.cs`;
const UP = `${ROOT}/database/migrations/V1/V1-RMD-645/194-audit-bulk-clear-guard.up.sql`;
const LOG = `${ROOT}/evidence/V1-RMD-645/mutations.log`;
const TEST = `${ROOT}/tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj`;
writeFileSync(LOG, '');
const files = { [DISP]: join(tmpdir(), 'disp.orig'), [PROG]: join(tmpdir(), 'prog.orig'), [UP]: join(tmpdir(), 'up.orig') };
for (const [f, o] of Object.entries(files)) copyFileSync(f, o);

const mutants = [];
const rep = (file, label, from, to) => mutants.push([file, label, (s) => { if (!s.includes(from)) throw new Error('no match: ' + label); return s.replace(from, to); }]);
rep(DISP, 'operator name not required', 'if (apply && string.IsNullOrWhiteSpace(operatorName))', 'if (false && string.IsNullOrWhiteSpace(operatorName))');
rep(DISP, 'advisory lock ignored', 'if (!(bool)(await guard.ExecuteScalarAsync(cancellationToken))!)', 'if (false && !(bool)(await guard.ExecuteScalarAsync(cancellationToken))!)');
rep(DISP, 'operator not recorded', ", 'operator', @operator));", '));');
rep(PROG, 'warning exit code lost', 'return (int)HostExitCode.CompletedWithWarnings;', 'return (int)HostExitCode.Success;');
rep(PROG, 'operator not required in command', 'if (apply && string.IsNullOrWhiteSpace(operatorName))', 'if (false && string.IsNullOrWhiteSpace(operatorName))');
rep(PROG, 'raw exception detail printed', '$"AUDIT-DISPOSAL: işlem tamamlanamadı ({ex.GetType().Name}).', '$"AUDIT-DISPOSAL: işlem tamamlanamadı ({ex.Message}).');
rep(PROG, 'run-active message lost', 'AUDIT-DISPOSAL: başka bir imha çalışması sürüyor', 'AUDIT-DISPOSAL: hata');
rep(UP, 'guard only on parent', "SELECT audit.guard_audit_partition(i.inhrelid::regclass)\nFROM pg_inherits i\nWHERE i.inhparent = 'audit.audit_events'::regclass;", 'SELECT 1;');
rep(UP, 'guard function does not raise', "RAISE EXCEPTION 'audit_events table is append-only. TRUNCATE is strictly forbidden.';", 'NULL;');

let survived = 0;
for (const [file, label, fn] of mutants) {
  try {
    const original = readFileSync(files[file], 'utf8');
    const applied = fn(original);
    writeFileSync(file, applied);
    let out = '';
    try {
      out = execFileSync(`${process.env.HOME || process.env.USERPROFILE}/.dotnet/dotnet.exe`, ['test', TEST, '--nologo', '--filter', 'FullyQualifiedName~AuditDisposal'], { encoding: 'utf8', env: { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' }, stdio: ['ignore', 'pipe', 'pipe'], maxBuffer: 64 * 1024 * 1024 });
    } catch (e) { out = String(e.stdout || '') + String(e.stderr || ''); }
    const compileErr = /error CS/.test(out);
    const killed = /Başarısız!/.test(out) || compileErr;
    if (!killed) survived++;
    appendFileSync(LOG, `${killed ? (compileErr ? 'COMPILE ' : 'KILLED  ') : 'SURVIVED'} ${label}\n`);
  } catch (e) { appendFileSync(LOG, `ERROR    ${label}: ${e.message}\n`); }
  finally { copyFileSync(files[file], file); }
}
for (const [f, o] of Object.entries(files)) appendFileSync(LOG, `restore ${f.split('/').pop()}: ${Buffer.compare(readFileSync(f), readFileSync(o)) === 0 ? 'identical' : 'DIFFERENT'}\n`);
appendFileSync(LOG, `mutants: ${mutants.length}, survived: ${survived}\n`);
