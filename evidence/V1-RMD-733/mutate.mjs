import fs from 'node:fs';
import { execSync } from 'node:child_process';
const APP = 'src/Host/Experience/Billing/BillingSplitApplication.cs';
const REC = 'src/Host/DualScreen/DualScreenApplication.Receipt.cs';
const dn = `${process.env.HOME}/.dotnet/dotnet.exe`;
const B = (f) => `${dn} test tests/Host/Experience/Billing -nologo --filter "${f}"`;
const ERR = B('FullyQualifiedName~ARefusedDiscountRequest|FullyQualifiedName~ValidationAndLookupFailures');
const M = [
  ['M1-grant-denied-message-english', APP, '"GRANT_DENIED", "İndirim isteği reddedildi."', '"GRANT_DENIED", "Discount request was denied."', ERR],
  ['M2-order-state-back-to-400', APP, 'UnsupportedOrderStateException => (409,', 'UnsupportedOrderStateException => (400,', ERR],
  ['M3-not-found-always-bill', APP, '$"İstenen {notFound.Resource} bulunamadı."', '"İstenen hesap bulunamadı."', ERR],
  ['M4-validation-code-generic', APP, 'BillingValidationException validation => (400, validation.Code, validation.UserMessage)', 'BillingValidationException validation => (400, "VALIDATION_FAILED", validation.UserMessage)', ERR],
  ['M5-refund-input-code-generic', APP, '400, refundInput.Code, refundInput.UserMessage', '400, "REFUND_INPUT_INVALID", refundInput.UserMessage', B('FullyQualifiedName~PaymentRefundHttpTests')],
  ['M6-rate-limit-not-attached', APP, 'builder.Metadata.Add(new EnableRateLimitingAttribute(policy));', '', B('FullyQualifiedName~EveryBillingEndpointCarries')],
  ['M7-receipt-404-without-envelope', REC, '"NOT_FOUND", "İstenen hesap bulunamadı."', '"NOT_FOUND", "x"', `${dn} test tests/Host/Experience/PaymentTender -nologo --filter "FullyQualifiedName~AnUnknownBillIsNotFound"`],
];
const only = process.argv[2];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
const log = 'evidence/V1-RMD-733/mutations.log';
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
