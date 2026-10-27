import fs from 'node:fs';
import { execSync } from 'node:child_process';
const BILL = 'src/Modules/Billing/BillFoundation/Bill.cs';
const MATH = 'src/Modules/Billing/BillFoundation/BillMath.cs';
const TB = 'src/Modules/Billing/Adjustments/TaxBreakdown.cs';
const REC = 'src/Host/DualScreen/DualScreenApplication.Receipt.cs';
const JS = 'src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js';
const dn = `${process.env.HOME}/.dotnet/dotnet.exe`;
const ADJ = { cmd: `${dn} test tests/Modules/Billing/Adjustments -nologo --filter FullyQualifiedName~DomainTests`, cwd: '.' };
const SPL = { cmd: `${dn} test tests/Modules/Billing/SplitDesign -nologo --filter FullyQualifiedName~DomainTests`, cwd: '.' };
const REPT = { cmd: `${dn} test tests/Host/Experience/PaymentTender -nologo --filter FullyQualifiedName~TheReceiptFoots`, cwd: '.' };
const WEB = { cmd: 'npx vitest run split-payment-receipt', cwd: 'tests/Clients/StaticApps' };
const M = [
  ['M1-discount-tax-uses-rounded-rate', BILL, 'TaxTotal / net * 100m : 0m', 'Math.Round(TaxTotal / net * 100m, 2) : 0m', ADJ],
  ['M2-tax-remainder-dropped', MATH, '.Take(missingCents))', '.Take(0))', SPL],
  ['M3-receipt-subtotal-back-to-line-subtotal', REC, 'bill.PayableAmount,\n                    summary.AdjustedPayableAmount,', 'summary.OriginalSubtotal,\n                    summary.AdjustedPayableAmount,', REPT],
  ['M4-receipt-without-tax-lines', REC, 'TaxBreakdown.Build(bill, summary)\n', 'TaxBreakdown.Build(bill, summary).Take(0)\n', REPT],
  ['M5-tips-counted-in-tax-gross', TB, 'summary.AdjustedPayableAmount - summary.TotalTips', 'summary.AdjustedPayableAmount', ADJ],
  ['M6-line-discount-not-reported', REC, 'i.DiscountAmount))', '0m))', REPT],
  ['M7-tax-table-not-printed', JS, '      receiptTaxHtml(receipt) +\n', '', WEB],
  ['M8-line-discount-row-not-printed', JS, 'item.lineDiscount > 0', 'false', WEB],
];
const only = process.argv[2];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres', PATH: `${process.env.PATH};C:\Program Files\nodejs` };
const root = process.cwd();
const log = 'evidence/V1-RMD-730/mutations.log';
for (const [id, file, from, to, t] of M) {
  if (only && only !== id) continue;
  const orig = fs.readFileSync(file, 'utf8');
  const crlf = orig.includes('\r\n');
  const f = crlf ? from.replace(/\n/g, '\r\n') : from;
  const x = crlf ? to.replace(/\n/g, '\r\n') : to;
  if (!orig.includes(f)) { fs.appendFileSync(log, `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(f, x));
  let verdict;
  try { execSync(t.cmd, { stdio: 'pipe', env, cwd: `${root}/${t.cwd}`, timeout: 600000 }); verdict = 'SURVIVED'; }
  catch (e) { verdict = /error CS|error CA/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync(log, `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync(log, 'DONE\n');
