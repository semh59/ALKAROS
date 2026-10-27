import fs from 'node:fs';
import { execSync } from 'node:child_process';
const dn = `${process.env.HOME}/.dotnet/dotnet.exe`;
const BILL = 'src/Modules/Billing/BillFoundation/Bill.cs';
const APP = 'src/Host/Experience/Billing/BillingSplitApplication.cs';
const SWP = 'src/Host/Experience/Billing/BillClosureSweeper.cs';
const T = (dir, f) => `${dn} test ${dir} -nologo --filter "${f}"`;
const MAP = T('tests/Host/Experience/Billing', 'FullyQualifiedName~BillingSplitErrorMappingTests');
const SWEEP = T('tests/Host/Experience/PaymentTender', 'FullyQualifiedName~FailedToCloseIsClosedByTheSweep');
const M = [
  ['M1-paid-to-cancelled-forbidden', BILL, 'or BillState.Reopened or BillState.Paid,', 'or BillState.Reopened,', T('tests/Modules/Billing/BillFoundation', 'FullyQualifiedName~ASettledBillThatIsRefundedInFull')],
  ['M2-cancel-is-500', APP, 'OperationCanceledException => (499,', 'OperationCanceledException => (500,', MAP],
  ['M3-lock-faults-not-mapped', APP, 'PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.LockNotAvailable', 'PostgresErrorCodes.DeadlockDetected', MAP],
  ['M4-sweep-skips-open-bills', SWP, "b.status NOT IN ('Paid', 'Cancelled')", "b.status IN ('Cancelled')", SWEEP],
  ['M5-sweep-counts-nothing-closed', SWP, 'if (result.Outcome != BillClosureOutcome.Closed)', 'if (result.Outcome == BillClosureOutcome.Closed)', SWEEP],
  ['M6-sweeper-not-registered', APP, 'services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, BillClosureSweeper>());', '', SWEEP],
];
const only = process.argv[2];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
const log = 'evidence/V1-RMD-736/mutations.log';
for (const [id, file, from, to, cmd] of M) {
  if (only && only !== id) continue;
  const orig = fs.readFileSync(file, 'utf8');
  if (!orig.includes(from)) { fs.appendFileSync(log, `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(from, to));
  let verdict;
  try { execSync(cmd, { stdio: 'pipe', env, timeout: 900000 }); verdict = 'SURVIVED'; }
  catch (e) { verdict = /error CS|error CA/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync(log, `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync(log, 'DONE\n');
