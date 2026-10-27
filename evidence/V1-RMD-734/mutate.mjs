import fs from 'node:fs';
import { execSync } from 'node:child_process';
const P = 'src/Clients/Cashier/wwwroot/payments/';
const SP = P + 'split-payment/split-payment.js';
const CS = P + 'cash-session/cash-session.js';
const CA = P + 'customer-accounts/customer-accounts.js';
const M = [
  ['M1-tender-key-cleared-on-5xx', SP, 'if (result.status < 500) state.tenderIdempotencyKey = null;', 'state.tenderIdempotencyKey = null;'],
  ['M2-bootstrap-any-failure-to-login', SP, "if (result.status === 401 || result.status === 403) {\n          state.phase = 'login';", "if (true) {\n          state.phase = 'login';"],
  ['M3-no-timeout', SP, 'signal: typeof AbortSignal', 'xsignal: typeof AbortSignal'],
  ['M4-last-share-not-remainder', SP, 'if (!(state.splitLeft > 1)) return remainingAmount();', 'if (true) return Math.round((remainingAmount() / Math.max(state.splitLeft, 2)) * 100) / 100;'],
  ['M5-no-next-share-after-tender', SP, 'state.amountDraft = nextShare().toFixed(2);\n        render();', 'render();'],
  ['M6-no-manager-wording', SP, 'müdüre yönlendirin', 'devam edin'],
  ['M7-cash-session-no-loaderror', CS, "state.phase = 'loadError';\n        }", "state.phase = 'login';\n        }"],
  ['M8-accounts-bootstrap-500-to-login', CA, "if (result.status === 401 || result.status === 403) {\n          state.phase = 'login';", "if (true) {\n          state.phase = 'login';"],
  ['M9-focus-ring-removed', P + 'split-payment/split-payment.css', '.sp-amount-field:focus-within { outline: 3px solid', '.sp-amount-field:focus-within { outline: 0 solid'],
  ['M10-cash-time-device-zone', CS, "timeZone: 'Europe/Istanbul'", "tz: 'Europe/Istanbul'"],
];
const only = process.argv[2];
const log = 'evidence/V1-RMD-734/mutations.log';
for (const [id, file, from, to] of M) {
  if (only && only !== id) continue;
  const orig = fs.readFileSync(file, 'utf8');
  const f = orig.includes(from) ? from : from.replace(/\n/g, '\r\n');
  if (!orig.includes(f)) { fs.appendFileSync(log, `${id} NOT-APPLIED\n`); continue; }
  const t = f === from ? to : to.replace(/\n/g, '\r\n');
  fs.writeFileSync(file, orig.replace(f, t));
  let verdict;
  try { execSync('npx vitest run cashier-client-resilience', { cwd: 'tests/Clients/StaticApps', stdio: 'pipe', timeout: 300000 }); verdict = 'SURVIVED'; }
  catch { verdict = 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync(log, `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync(log, 'DONE\n');
