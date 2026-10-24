import fs from 'node:fs';
import { execSync } from 'node:child_process';
const DN = 'database/migrations/V1/V1-RMD-724/206-orders-check-rollback-safe.down.sql';
const CA = 'tools/consistency-audit/consistency_audit.py';
const RB = 'tests/Host/MigrationComposition --filter FullyQualifiedName~OrdersRollbackTests';
const M = [
  ['M1-nfc-rows-not-remapped', DN, "UPDATE orders.orders SET source = 'Qr' WHERE source = 'Nfc';", '', RB],
  ['M2-held-rows-not-remapped', DN, "UPDATE orders.order_items SET kitchen_state = 'Sent' WHERE kitchen_state = 'Held';", '', RB],
  ['M3-sent-checks-exception-removed', CA, '    ("Experience/Orders", "CashierHandoffStore.cs", "table_sent_checks"),\n', '', 'py'],
  ['M4-merges-exception-removed', CA, '    ("Experience/Orders", "OrderSettlementService.cs", "table_merges"),\n', '', 'py'],
  ['M5-tables-write-flagged', CA, 'if relation != "tables" and (area', 'if relation == "tables" and (area', 'py'],
];
const only = process.argv[2];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
for (const [id, file, from, to, target] of M) {
  if (only && only !== id) continue;
  const orig = fs.readFileSync(file, 'utf8');
  const crlf = orig.includes('\r\n');
  const f = crlf ? from.replace(/\n/g, '\r\n') : from;
  const t = crlf ? to.replace(/\n/g, '\r\n') : to;
  if (!orig.includes(f)) { fs.appendFileSync('evidence/V1-RMD-724/mutations.log', `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(f, t));
  let verdict;
  const cmd = target.startsWith('py') ? 'python tools/consistency-audit/consistency_audit.py' : `${process.env.HOME}/.dotnet/dotnet.exe test ${target} -nologo`;
  try {
    const out = String(execSync(cmd, { stdio: 'pipe', env }));
    verdict = target.startsWith('py') && !out.includes('clean') ? 'KILLED' : 'SURVIVED';
  } catch (e) { verdict = /error CS|error CA/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync('evidence/V1-RMD-724/mutations.log', `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync('evidence/V1-RMD-724/mutations.log', 'DONE\n');
