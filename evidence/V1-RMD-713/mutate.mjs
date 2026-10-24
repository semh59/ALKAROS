import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const p = (rel) => `${root}/${rel}`;
const pos = "src/Clients/PosTerminal/src";
const workspace = p(`${pos}/routes/workspace.tsx`);
const station = p(`${pos}/routes/ReservationStation.tsx`);
const tableWs = p(`${pos}/features/tables/TableWorkspace.tsx`);
const floorPlan = p(`${pos}/features/tables/FloorPlanWorkspace.tsx`);
const tablesCss = p(`${pos}/features/tables/tables.css`);
const floorCss = p(`${pos}/features/tables/floorPlan.css`);
const tablesJs = p("src/Clients/WaiterPwa/wwwroot/js/screens/tables.js");
const appJs = p("src/Clients/WaiterPwa/wwwroot/waiter-app.js");
const transferJs = p("src/Clients/WaiterPwa/wwwroot/js/sheets/transfer.js");
const log = p("evidence/V1-RMD-713/mutations.log");

const vit = (files) => `export PATH="$PATH:/c/Program Files/nodejs"; cd src/Clients/PosTerminal && npx vitest run ${files}`;
const routeTests = vit("src/routes/workspace.test.tsx src/routes/ReservationStation.test.tsx");
const tableTests = vit("src/features/tables/TableWorkspace.test.tsx");
const floorTests = vit("src/features/tables/FloorPlanWorkspace.test.tsx");
const styleTests = vit("src/features/tables/tablesStyle.test.ts");
const waiterTests = "python -m pytest tests/Clients/WaiterPwa/Frontend/test_waiter_tables_reliability.py -q";

const mutants = [
  { id: "W1", file: workspace, from: 'if (!silent) {\n      setState("loading");', to: 'if (true) {\n      setState("loading");', cmd: routeTests, note: "background refresh swaps the screen for loading" },
  { id: "W2", file: workspace, from: "if (silent) {\n        if (status === 401)", to: "if (false) {\n        if (status === 401)", cmd: routeTests, note: "a failed background refresh replaces the tables with an error screen" },
  { id: "W3", file: workspace, from: 'if (state === "loading" || state === "unauthorized") return;', to: 'if (state !== "ready" && state !== "empty") return;', cmd: routeTests, note: "polling stops after a failure" },
  { id: "W4", file: workspace, from: "if (!keepOnFailure) setFloorPlan(undefined);", to: "setFloorPlan(undefined);", cmd: routeTests, note: "failed plan refresh drops the floor plan" },
  { id: "W5", file: workspace, from: "try { await action(); } finally { await load(true); }", to: "await action(); await load(true);", cmd: routeTests, note: "no reload after a refused action" },
  { id: "W6", file: workspace, from: "onUpdateTable={canManage && !reservationOnly ?", to: "onUpdateTable={canManage ?", cmd: routeTests, note: "station staff can edit tables" },
  { id: "W7", file: workspace, from: 'state === "ready" && !reservationOnly && <section className="table-order-bridge"', to: 'state === "ready" && <section className="table-order-bridge"', cmd: routeTests, note: "station shows the order bridge" },
  { id: "W8", file: station, from: "canManage reservationOnly />", to: "canManage={false} reservationOnly />", cmd: routeTests, note: "station cannot act" },
  { id: "F1", file: floorPlan, from: '&& reason.code === "CONCURRENT_MODIFICATION";', to: "&& false;", cmd: floorTests, note: "floor plan conflict never detected" },
  { id: "T1", file: tableWs, from: '&& table.status === "Available");\n  const mergeTargets', to: ");\n  const mergeTargets", cmd: tableTests, note: "transfer targets not filtered by status" },
  { id: "T2", file: tableWs, from: '(table.status === "Available" || table.status === "Occupied"));', to: "true);", cmd: tableTests, note: "merge targets not filtered by status" },
  { id: "T3", file: tableWs, from: "setReasonChoice(reasonPresets[nextAction]?.[0] ?? OTHER_REASON);", to: "setReasonChoice(OTHER_REASON);", cmd: tableTests, note: "no prepared reason" },
  { id: "T4", file: tableWs, from: "reasonChoice === OTHER_REASON ? reason.trim() : reasonChoice;", to: "reasonChoice === OTHER_REASON ? reason.trim() : reason.trim();", cmd: tableTests, note: "prepared reason ignored" },
  { id: "T5", file: tableWs, from: "setFormErrors([isConcurrencyConflict(reasonValue)", to: "setFormErrors([false", cmd: tableTests, note: "conflict text never shown" },
  { id: "T6", file: tableWs, from: '{refreshError && <p className="table-workspace__refresh-error"', to: '{false && <p className="table-workspace__refresh-error"', cmd: tableTests, note: "refresh banner hidden" },
  { id: "T7", file: tableWs, from: "{commands.some((command) => !isClientExecutableAction(command, table)) &&", to: "{false &&", cmd: tableTests, note: "dimmed action reason hidden" },
  { id: "T8", file: tableWs, from: "· {tableStatusLabels[actionTable.status]}</p>}", to: "· {tableStatusLabels[actionTable.status]} · v{actionTable.rowVersion}</p>}", cmd: tableTests, note: "row version shown in the dialog" },
  { id: "T9", file: tableWs, from: "transferTargets.length === 0\n", to: "false\n", cmd: tableTests, note: "no message when no free table" },
  { id: "C1", file: tablesCss, from: ".table-workspace__kicker { color: var(--table-accent-text, #00627a);", to: ".table-workspace__kicker { color: var(--ds-color-accent);", cmd: styleTests, note: "kicker text back to the light accent colour" },
  { id: "C2", file: floorCss, from: "color: #4d5d64; font-size: .75rem;", to: "color: #4d5d64; font-size: .5rem;", cmd: styleTests, note: "tiny floor plan label" },
  { id: "P1", file: tablesJs, from: "openedAt: table.occupiedSince || null,", to: "openedAt: table.currentOrderOpenedAt || null,", cmd: waiterTests, note: "waiter age badge reads the wrong field" },
  { id: "P2", file: tablesJs, from: "state.tablesFailed = !result.ok;", to: "state.tablesFailed = false;", cmd: waiterTests, note: "waiter list failure not flagged" },
  { id: "P3", file: appJs, from: "el.tablesScreen.dataset.state === 'on'", to: "true", cmd: waiterTests, note: "waiter polls while not on the tables screen" },
  { id: "P4", file: transferJs, from: "if (current) state.table = current;", to: "", cmd: waiterTests, note: "transfer conflict keeps the stale table" },
];

const only = process.argv.slice(2);
for (const m of mutants) {
  if (only.length && !only.includes(m.id)) continue;
  const original = readFileSync(m.file, "utf8");
  const nl = original.includes("\r\n") ? "\r\n" : "\n";
  const from = m.from.replace(/\n/g, nl);
  if (!original.includes(from)) { appendFileSync(log, `${m.id} NOT-APPLIED ${m.note}\n`); continue; }
  writeFileSync(m.file, original.replace(from, m.to.replace(/\n/g, nl)));
  let result;
  try { execSync(m.cmd, { cwd: root, shell: "bash", stdio: "pipe", timeout: 600000 }); result = "SURVIVED"; }
  catch (e) { result = /error TS|SyntaxError|Transform failed/.test(`${e.stdout}${e.stderr}`) ? "BUILD-ERROR" : "KILLED"; }
  finally { writeFileSync(m.file, original); }
  appendFileSync(log, `${m.id} ${result} restored=${readFileSync(m.file, "utf8") === original} ${m.note}\n`);
}
