import fs from 'node:fs';
import { execSync } from 'node:child_process';
const ST = 'src/Host/Experience/Billing/BillingSplitStore.cs';
const SQL = 'database/migrations/V1/V1-RMD-732/209-bills-integrity.up.sql';
const dn = `${process.env.HOME}/.dotnet/dotnet.exe`;
const POINTER = `${dn} test tests/Host/Experience/Billing -nologo --filter FullyQualifiedName~AFailedTablePointerWrite`;
const SCHEMA = `${dn} test tests/Host/MigrationComposition -nologo --filter FullyQualifiedName~TheBillsIntegrityRules`;
const M = [
  ['M1-pointer-failure-not-logged', ST, 'LogTablePointerNotWritten(_logger, billId, order.TableId.Value, ex);', '_ = ex;', POINTER],
  ['M2-nonnegative-check-dropped', SQL, "IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_bills_nonnegative_amounts') THEN", 'IF false THEN', SCHEMA],
  ['M3-cancelled-by-fk-dropped', SQL, "IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_bills_cancelled_by') THEN", 'IF false THEN', SCHEMA],
  ['M4-redundant-index-kept', SQL, 'DROP INDEX IF EXISTS billing.ix_bill_items_bill;', '', SCHEMA],
];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
const log = 'evidence/V1-RMD-732/mutations.log';
for (const [id, file, from, to, cmd] of M) {
  const orig = fs.readFileSync(file, 'utf8');
  const crlf = orig.includes('\r\n');
  const f = crlf ? from.replace(/\n/g, '\r\n') : from;
  if (!orig.includes(f)) { fs.appendFileSync(log, `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(f, to));
  let verdict;
  try { execSync(cmd, { stdio: 'pipe', env, timeout: 900000 }); verdict = 'SURVIVED'; }
  catch (e) { verdict = /error CS|error CA/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync(log, `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync(log, 'DONE\n');
