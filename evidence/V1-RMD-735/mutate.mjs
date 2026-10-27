import fs from 'node:fs';
import { execSync } from 'node:child_process';
const W = 'src/Clients/PosTerminal/src/features/billing/BillSplitWorkspace.tsx';
const R = 'src/Clients/PosTerminal/src/routes/workspace.tsx';
const S = 'src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js';
const POS = { cwd: 'src/Clients/PosTerminal', cmd: 'npx vitest run src/features/billing/BillSplitWorkspace.recovery src/routes/workspace' };
const CASH = { cwd: 'tests/Clients/StaticApps', cmd: 'npx vitest run cashier-client-resilience' };
const M = [
  ['M1-dirty-blocks-design-sync', W, 'setDesign(suppliedDesign);\n    if (dirty) return;', 'if (dirty) return;\n    setDesign(suppliedDesign);', POS],
  ['M2-any-409-is-conflict', W, 'status === 409 && code !== "ITEM_SPLIT_ON_ADJUSTED_BILL"', 'status === 409', POS],
  ['M3-empty-cell-becomes-zero', W, 'return raw === "" ? rest : { ...rest, [key]: parse(raw) };', 'return { ...rest, [key]: parse(raw) };', POS],
  ['M4-no-owner-message', W, 'if (owners.length === 0) return <NoOwners />;\n  return <section className="bill-split__owner-picker"', 'return <section className="bill-split__owner-picker"', POS],
  ['M5-clear-without-confirm', W, 'onClick={() => setConfirmClear(true)}', 'onClick={() => void clear()}', POS],
  ['M6-adjusted-note-removed', W, '{adjusted && <p', '{false && <p', POS],
  ['M7-load-reruns-on-bill-id', R, '  }, [orderParam, terminalId]);\n\n  useEffect(() => { void load(); }, [load]);', '  }, [orderParam, terminalId, billId]);\n\n  useEffect(() => { void load(); }, [load]);', POS],
  ['M8-share-ignored', S, "state.amountDraft = Math.min(share.amount, remainingAmount()).toFixed(2);", "state.amountDraft = remainingAmount().toFixed(2);", CASH],
  ['M9-owner-totals-not-summed', S, 'totals[id] += Number(allocation.amount) || 0;', 'totals[id] = Number(allocation.amount) || 0;', CASH],
];
const only = process.argv[2];
const log = 'evidence/V1-RMD-735/mutations.log';
for (const [id, file, from, to, run] of M) {
  if (only && only !== id) continue;
  const orig = fs.readFileSync(file, 'utf8');
  const f = orig.includes(from) ? from : from.replace(/\n/g, '\r\n');
  if (!orig.includes(f)) { fs.appendFileSync(log, `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(f, f === from ? to : to.replace(/\n/g, '\r\n')));
  let verdict;
  try { execSync(run.cmd, { cwd: run.cwd, stdio: 'pipe', timeout: 300000 }); verdict = 'SURVIVED'; }
  catch { verdict = 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync(log, `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync(log, 'DONE\n');
