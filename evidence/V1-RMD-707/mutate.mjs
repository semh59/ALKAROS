import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const cashier = `${root}/src/Clients/Cashier/wwwroot/cashier-app.js`;
const waiter = `${root}/src/Clients/WaiterPwa/wwwroot/waiter-app.js`;
const menu = `${root}/src/Clients/WaiterPwa/wwwroot/js/screens/menu.js`;
const state = `${root}/src/Clients/WaiterPwa/wwwroot/js/state.js`;
const log = `${root}/evidence/V1-RMD-707/mutations.log`;
const cmd = "python -m pytest tests/Clients/Cashier/Frontend/test_cashier_catalog_search.py tests/Clients/WaiterPwa/Frontend/test_waiter_catalog_retry.py -q";

const mutants = [
  { id: "M1", file: cashier, from: "const query = searchQuery.trim().toLocaleLowerCase('tr-TR');", to: "const query = searchQuery.trim().toLowerCase();", note: "query lowercased without Turkish rules" },
  { id: "M2", file: cashier, from: "p.name.toLocaleLowerCase('tr-TR').includes(query)", to: "p.name.toLowerCase().includes(query)", note: "name compared without Turkish rules" },
  { id: "M3", file: cashier, from: "if (filtered.length === 0) {", to: "if (false) {", note: "no empty message" },
  { id: "M4", file: waiter, from: "state.catalogFailed = true;", to: "", note: "failure never flagged" },
  { id: "M5", file: waiter, from: "state.catalogFailed = false;", to: "", note: "flag never cleared" },
  { id: "M6", file: waiter, from: "if (event.target.closest('[data-retry-catalog]')) {", to: "if (false) {", note: "retry click ignored" },
  { id: "M7", file: menu, from: "state.catalogFailed\n      ?", to: "false\n      ?", note: "failed menu shows the empty message" },
  { id: "M8", file: state, from: "  catalogFailed: false,", to: "", note: "state field missing" },
];

for (const m of mutants) {
  const original = readFileSync(m.file, "utf8");
  const nl = original.includes("\r\n") ? "\r\n" : "\n";
  const from = m.from.replace(/\n/g, nl);
  if (!original.includes(from)) { appendFileSync(log, `${m.id} NOT-APPLIED ${m.note}\n`); continue; }
  writeFileSync(m.file, original.replace(from, m.to.replace(/\n/g, nl)));
  let result;
  try { execSync(cmd, { cwd: root, shell: "bash", stdio: "pipe", timeout: 120000 }); result = "SURVIVED"; }
  catch { result = "KILLED"; }
  finally { writeFileSync(m.file, original); }
  appendFileSync(log, `${m.id} ${result} restored=${readFileSync(m.file, "utf8") === original} ${m.note}\n`);
}
