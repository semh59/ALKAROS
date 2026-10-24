import fs from 'node:fs';
import { execSync } from 'node:child_process';
const J = 'src/Clients/Cashier/wwwroot/cashier-app.js';
const X = 'src/Clients/PosTerminal/src/routes/Cashier.tsx';
const M = [
  ['M1-fresh-id-every-press', J, "state.pendingDispatch || {", "{", 'static'],
  ['M2-release-on-every-submit-failure', J, "if (isFinalRejection(submitResponse.status, failure.code)) {", "if (true) {", 'static'],
  ['M3-all-409-retryable', J, "if (status === 409) return code !== 'CONCURRENCY_CONFLICT';", "if (status === 409) return false;", 'static'],
  ['M4-server-message-ignored', J, "(error && error.message) || describeHttpFailure", "describeHttpFailure", 'static'],
  ['M5-pending-kept-after-success', J, "state.pendingDispatch = null;\n      await releaseSharedTable", "await releaseSharedTable", 'static'],
  ['M6-full-reload-after-action', X, "await reloadOrder();\n      if (success) {", "await reload();\n      if (success) {", 'react'],
];
const only = process.argv[2];
const env = { ...process.env, PATH: process.env.PATH + ';/c/Program Files/nodejs' };
for (const [id, file, from, to, kind] of M) {
  if (only && only !== id) continue;
  const orig = fs.readFileSync(file, 'utf8');
  if (!orig.includes(from)) { fs.appendFileSync('evidence/V1-RMD-719/mutations.log', `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(from, to));
  const [cwd, cmd] = kind === 'static' ? ['tests/Clients/StaticApps', 'npx vitest run cashier-app'] : ['src/Clients/PosTerminal', 'npx vitest run src/routes/Cashier.sale-refresh'];
  let verdict;
  try { execSync(cmd, { cwd, stdio: 'pipe', env }); verdict = 'SURVIVED'; } catch { verdict = 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync('evidence/V1-RMD-719/mutations.log', `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
