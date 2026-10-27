import fs from 'node:fs';
import { execSync } from 'node:child_process';
const SQL = 'database/migrations/V1/V1-RMD-731/208-line-discount-includes-modifiers.up.sql';
const dn = `${process.env.HOME}/.dotnet/dotnet.exe`;
const cmd = `${dn} test tests/Host/Experience/Orders/Comp -nologo --filter FullyQualifiedName~AnItemWithAPricedModifier`;
const M = [
  ['M1-order-items-upper-bound-kept', SQL, 'CHECK (\n        discount_amount >= 0\n        AND tax_amount >= 0', 'CHECK (\n        discount_amount >= 0\n        AND discount_amount <= round(quantity * unit_price, 2)\n        AND tax_amount >= 0', false],
  ['M2-bill-items-upper-bound-kept', SQL, 'ALTER TABLE billing.bill_items\n    ADD CONSTRAINT ck_bill_items_nonnegative_amounts\n    CHECK (\n        discount_amount >= 0\n', 'ALTER TABLE billing.bill_items\n    ADD CONSTRAINT ck_bill_items_nonnegative_amounts\n    CHECK (\n        discount_amount >= 0\n        AND discount_amount <= round(quantity * unit_price, 2)\n', false],
];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
const log = 'evidence/V1-RMD-731/mutations.log';
for (const [id, file, from, to] of M) {
  const orig = fs.readFileSync(file, 'utf8');
  const crlf = orig.includes('\r\n');
  const f = crlf ? from.replace(/\n/g, '\r\n') : from;
  const x = crlf ? to.replace(/\n/g, '\r\n') : to;
  if (!orig.includes(f)) { fs.appendFileSync(log, `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(f, x));
  let verdict;
  try { execSync(cmd, { stdio: 'pipe', env, timeout: 600000 }); verdict = 'SURVIVED'; }
  catch (e) { verdict = /error CS|error CA/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync(log, `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync(log, 'DONE\n');
