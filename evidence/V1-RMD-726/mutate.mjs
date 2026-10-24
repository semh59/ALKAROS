import fs from 'node:fs';
import { execSync } from 'node:child_process';
const CAL = 'src/Modules/Billing/PaymentClosure/BillPaymentClosureCalculator.cs';
const APP = 'src/Host/Experience/Billing/BillingSplitApplication.cs';
const JS = 'src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js';
const dn = `${process.env.HOME}/.dotnet/dotnet.exe`;
const MOD = { cmd: `${dn} test tests/Modules/Billing/PaymentClosure -nologo`, cwd: '.' };
const HOST = { cmd: `${dn} test tests/Host/Experience/Billing -nologo --filter FullyQualifiedName~ADiscountThatTakesThePayableToZero`, cwd: '.' };
const WEB = { cmd: 'npx vitest run split-payment-failures', cwd: 'tests/Clients/StaticApps' };
const M = [
  ['M1-zero-payable-never-satisfied', CAL, 'var paymentSatisfied = allocatedTotal >= adjustedPayable;', 'var paymentSatisfied = adjustedPayable > 0 && allocatedTotal >= adjustedPayable;', MOD],
  ['M2-zero-discount-does-not-close', APP, 'if (summary.AdjustedPayableAmount <= 0m)', 'if (summary.AdjustedPayableAmount <= -1m)', HOST],
  ['M3-full-discount-not-confirmed', JS, 'if (takeOff >= remainingAmount() - 0.004 && !state.discountConfirm) {', 'if (takeOff >= remainingAmount() + 1e9 && !state.discountConfirm) {', WEB],
  ['M4-small-discount-always-asks', JS, 'if (takeOff >= remainingAmount() - 0.004 && !state.discountConfirm) {', 'if (takeOff >= -1 && !state.discountConfirm) {', WEB],
];
const only = process.argv[2];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres', PATH: `${process.env.PATH};C:\\Program Files\\nodejs` };
const root = process.cwd();
for (const [id, file, from, to, t] of M) {
  if (only && only !== id) continue;
  const orig = fs.readFileSync(file, 'utf8');
  const crlf = orig.includes('\r\n');
  const f = crlf ? from.replace(/\n/g, '\r\n') : from;
  const x = crlf ? to.replace(/\n/g, '\r\n') : to;
  if (!orig.includes(f)) { fs.appendFileSync('evidence/V1-RMD-726/mutations.log', `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(f, x));
  let verdict;
  try { execSync(t.cmd, { stdio: 'pipe', env, cwd: `${root}/${t.cwd}`, timeout: 600000 }); verdict = 'SURVIVED'; }
  catch (e) { verdict = /error CS|error CA/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync('evidence/V1-RMD-726/mutations.log', `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync('evidence/V1-RMD-726/mutations.log', 'DONE\n');
