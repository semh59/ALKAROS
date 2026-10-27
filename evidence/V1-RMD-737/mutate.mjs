import fs from 'node:fs';
import { execSync } from 'node:child_process';
const dn = `${process.env.HOME}/.dotnet/dotnet.exe`;
const T = (dir, f) => `${dn} test ${dir} -nologo --filter "${f}"`;
const S = 'src/Modules/Billing/';
const H = 'src/Host/Experience/Billing/';
const ADJ = T('tests/Modules/Billing/Adjustments', 'FullyQualifiedName~AdjustmentsDomainTests');
const BF = T('tests/Modules/Billing/BillFoundation', 'FullyQualifiedName~BillDomainTests');
const SPL = T('tests/Modules/Billing/SplitDesign', 'FullyQualifiedName~SplitDesignDomainTests');
const CLO = T('tests/Modules/Billing/PaymentClosure', 'FullyQualifiedName~PaymentClosure');
const REF = T('tests/Host/Experience/Billing', 'FullyQualifiedName~PaymentRefundHttpTests');
const CAN = T('tests/Host/Experience/Billing', 'FullyQualifiedName~BillCancellationHttpTests');
const SPLH = T('tests/Host/Experience/Billing', 'FullyQualifiedName~ExactlyWhatWasAlreadyCollected|FullyQualifiedName~ATipOnASettledBill|FullyQualifiedName~TurnedTheVoluntaryTipOff');
// [id, file, from, to, command, nth occurrence (default 1)]
const M = [
  ['P1-discount-equal-to-payable-refused', S + 'Adjustments/AdjustmentCalculator.cs', 'if (discountGross > (totalBasePayable + feeGross))', 'if (discountGross >= (totalBasePayable + feeGross))', ADJ],
  ['P2-line-discounts-dropped', S + 'Adjustments/AdjustmentCalculator.cs', 'BillMath.RoundCurrency(bill.DiscountTotal + discountGross)', 'BillMath.RoundCurrency(discountGross)', ADJ],
  ['P3-adjusted-tax-unclamped', S + 'Adjustments/AdjustmentCalculator.cs', 'Math.Max(0m, bill.TaxTotal - discountTax + feeTax)', 'Math.Max(-999999m, bill.TaxTotal - discountTax + feeTax)', ADJ],
  ['P4-discount-tax-added', S + 'Adjustments/AdjustmentCalculator.cs', 'bill.TaxTotal - discountTax + feeTax', 'bill.TaxTotal + discountTax + feeTax', ADJ],
  ['P5-rate-100-refused-ctor', S + 'Adjustments/BillAdjustment.cs', 'rate.Value <= 0 || rate.Value > 100', 'rate.Value <= 0 || rate.Value >= 100', ADJ],
  ['P6-rate-100-refused-factory', S + 'Adjustments/BillAdjustment.cs', 'if (rate <= 0 || rate > 100)', 'if (rate <= 0 || rate >= 100)', ADJ],
  ['P7-half-kurus-to-even', S + 'BillFoundation/BillMath.cs', 'Math.Round(value, 2, MidpointRounding.AwayFromZero)', 'Math.Round(value, 2, MidpointRounding.ToEven)', BF],
  ['P8-zero-price-refused', S + 'BillFoundation/BillItem.cs', 'if (unitPrice < 0)', 'if (unitPrice <= 0)', BF],
  ['P9-custom-split-shortfall-allowed', S + 'SplitDesign/SplitEngine.cs', 'if (roundedAmounts.Sum() != totalPayable)', 'if (roundedAmounts.Sum() > totalPayable)', SPL],
  ['P10-reconciliation-not-a-blocker', S + 'PaymentClosure/BillPaymentClosureCalculator.cs', 'p.Status is PaymentStatus.Pending or PaymentStatus.ReconciliationRequired', 'p.Status is PaymentStatus.Pending', CLO],
  ['P11-no-retry', S + 'PaymentClosure/BillClosureService.cs', 'when (attempt < MaxAttempts)', 'when (attempt < 1)', CLO],
  ['P12-blockers-ignored', S + 'PaymentClosure/BillClosureService.cs', 'if (!projection.PaymentSatisfied || projection.Blockers.Count > 0)', 'if (!projection.PaymentSatisfied)', CLO],
  ['Q1-nothing-left-refundable', H + 'PaymentRefunds.cs', 'if (amount <= 0m)\n                throw new RefundPaymentNotRefundableException', 'if (amount < 0m)\n                throw new RefundPaymentNotRefundableException', REF],
  ['Q2-self-decision-allowed', H + 'PaymentRefunds.cs', 'if (request.RequestedBy == deciderId)', 'if (false)', REF],
  ['Q3-allowance-max-refused', H + 'PaymentRefunds.cs', 'if (amount < 0 || amount > MaxAllowance)', 'if (amount < 0 || amount >= MaxAllowance)', REF],
  ['Q4-allowance-zero-refused', H + 'PaymentRefunds.cs', 'if (amount < 0 || amount > MaxAllowance)', 'if (amount <= 0 || amount > MaxAllowance)', REF],
  ['Q5-partly-refunded-ignored', H + 'PaymentRefunds.cs', "status IN ('Approved', 'PartiallyRefunded', 'Pending', 'Unknown', 'ReconciliationRequired')", "status IN ('Approved', 'Pending', 'Unknown', 'ReconciliationRequired')", REF],
  ['Q6-unknown-ignored', H + 'PaymentRefunds.cs', "status IN ('Approved', 'PartiallyRefunded', 'Pending', 'Unknown', 'ReconciliationRequired')", "status IN ('Approved', 'PartiallyRefunded', 'Pending', 'ReconciliationRequired')", REF],
  ['Q7-in-flight-payment-ignored', H + 'BillCancellation.cs', "p.status IN ('Pending', 'Unknown', 'ReconciliationRequired')", "p.status IN ('Pending')", CAN],
  ['Q8-refunded-still-blocks', H + 'BillCancellation.cs', "ap.status <> 'Refunded'", "ap.status <> 'Cancelled'", CAN],
  ['Q9-discount-may-undercut-by-equal', H + 'BillingSplitStore.cs', 'if (summary.AdjustedPayableAmount < collected)', 'if (summary.AdjustedPayableAmount <= collected)', SPLH],
  ['Q10-tip-on-settled-bill', H + 'BillingSplitStore.cs', 'if (!IsDiscountable(bill.Status))', 'if (false)', SPLH, 3],
  ['Q11-tip-feature-ignored', H + 'BillingSplitStore.cs', 'if (_settings is not null', 'if (false', SPLH],
];
const only = process.argv[2];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
const log = 'evidence/V1-RMD-737/mutations.log';
for (const [id, file, from0, to0, cmd, nth = 1] of M) {
  if (only && !id.startsWith(only)) continue;
  const orig = fs.readFileSync(file, 'utf8');
  const crlf = orig.includes('\r\n');
  const from = crlf ? from0.replace(/\n/g, '\r\n') : from0;
  const to = crlf ? to0.replace(/\n/g, '\r\n') : to0;
  let idx = -1;
  for (let i = 0; i < nth; i++) idx = orig.indexOf(from, idx + 1);
  if (idx < 0) { fs.appendFileSync(log, `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.slice(0, idx) + to + orig.slice(idx + from.length));
  let verdict;
  try { execSync(cmd, { stdio: 'pipe', env, timeout: 900000 }); verdict = 'SURVIVED'; }
  catch (e) { verdict = /error CS|error CA/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync(log, `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync(log, 'DONE\n');
