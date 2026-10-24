import fs from 'node:fs';
import { execSync } from 'node:child_process';
const root = 'src/Clients/WaiterPwa/wwwroot/';
const M = [
  ['M1-409-always-transient', 'js/offline-queue.js', "if (status === 409) return errorCode === 'CONCURRENCY_CONFLICT';", "if (status === 409) return true;"],
  ['M2-conflict-not-kept', 'js/offline-queue.js', "return errorCode === 'CONCURRENCY_CONFLICT';", "return false;"],
  ['M3-code-ignored', 'js/offline-queue.js', "isTransientOrderStatus(result.status, result.data && result.data.error && result.data.error.code)", "isTransientOrderStatus(result.status)"],
  ['M4-delete-without-arming', 'js/sheets/failed-orders.js', "if (armedFailedId !== payloadId) {", "if (false) {"],
  ['M5-retry-keeps-failed', 'js/sheets/failed-orders.js', "state.failedOrders = state.failedOrders.filter((candidate) => candidate.id !== payloadId);\n  const {", "const {"],
  ['M6-retry-keeps-error', 'js/sheets/failed-orders.js', "const { rejectedAt, error, ...round } = payload;", "const round = payload;"],
  ['M7-reject-without-arming', 'js/sheets/pending-orders.js', "if (!accept && armedRejectId !== orderId) {", "if (false) {"],
];
const only = process.argv[2];
for (const [id, file, from, to] of M) {
  if (only && only !== id) continue;
  const path = root + file;
  const orig = fs.readFileSync(path, 'utf8');
  if (!orig.includes(from)) { fs.appendFileSync('evidence/V1-RMD-718/mutations.log', `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(path, orig.replace(from, to));
  let verdict;
  try { execSync('npx vitest run offline-queue failed-orders', { cwd: 'tests/Clients/StaticApps', stdio: 'pipe', env: { ...process.env, PATH: process.env.PATH + ';/c/Program Files/nodejs' } }); verdict = 'SURVIVED'; }
  catch { verdict = 'KILLED'; }
  fs.writeFileSync(path, orig);
  fs.appendFileSync('evidence/V1-RMD-718/mutations.log', `${id} ${verdict} restored=${fs.readFileSync(path, 'utf8') === orig}\n`);
}
