import fs from 'node:fs';
import { execSync } from 'node:child_process';
const ST = 'src/Host/Experience/Billing/PaymentRefunds.cs';
const REPO = 'src/Modules/Payments/PaymentAggregate/PostgresRefundRequestRepository.cs';
const dn = `${process.env.HOME}/.dotnet/dotnet.exe`;
const HOST = (f) => ({ cmd: `${dn} test tests/Host/Experience/Billing -nologo --filter "${f}"`, cwd: '.' });
const M = [
  ['M1-replay-lookup-skipped', ST, 'if (earlier is not null)', 'if (earlier is not null && false)', HOST('FullyQualifiedName~ARetriedRequestWithTheSameKey')],
  ['M2-reused-key-other-payment-accepted', ST, 'if (earlier.PaymentId != paymentId)', 'if (false)', HOST('FullyQualifiedName~ARetriedRequestWithTheSameKey')],
  ['M3-missing-key-accepted', ST, 'if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength)', 'if (key is null)', HOST('FullyQualifiedName~ARequestWithoutAnIdempotencyKey')],
  ['M4-request-not-audited', ST, 'await AuditStepAsync("refund.requested"', '// await AuditStepAsync("refund.requested"', HOST('FullyQualifiedName~TheRequestAndTheApprovalAreAudited')],
  ['M5-approval-not-audited', ST, 'await AuditStepAsync("refund.approved", request, deciderId', '// await AuditStepAsync("refund.approved", request, deciderId', HOST('FullyQualifiedName~TheRequestAndTheApprovalAreAudited')],
  ['M6-bill-filter-dropped', REPO, '(@bill::uuid IS NULL OR bill_id = @bill)', '(1 = 1)', HOST('FullyQualifiedName~TheListFiltersByBill')],
  ['M7-before-filter-dropped', REPO, '(@before::timestamptz IS NULL OR requested_at < @before)', '(1 = 1)', HOST('FullyQualifiedName~TheListFiltersByBill')],
];
const only = process.argv[2];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
const root = process.cwd();
for (const [id, file, from, to, t] of M) {
  if (only && only !== id) continue;
  const orig = fs.readFileSync(file, 'utf8');
  const crlf = orig.includes('\r\n');
  const f = crlf ? from.replace(/\n/g, '\r\n') : from;
  if (!orig.includes(f)) { fs.appendFileSync('evidence/V1-RMD-727/mutations.log', `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(f, to));
  let verdict;
  try { execSync(t.cmd, { stdio: 'pipe', env, cwd: `${root}/${t.cwd}`, timeout: 600000 }); verdict = 'SURVIVED'; }
  catch (e) { verdict = /error CS|error CA/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync('evidence/V1-RMD-727/mutations.log', `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync('evidence/V1-RMD-727/mutations.log', 'DONE\n');
