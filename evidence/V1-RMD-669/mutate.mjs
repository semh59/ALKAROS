import fs from "node:fs";
import { execSync } from "node:child_process";
const root = "D:/PROJECT/ALKAROS/src/Clients/PosTerminal";
const dec = "src/features/authorization-decisions/";
const staff = "src/features/management-staff-roles/";
const mutants = [
  { id: "E1", file: dec + "AuthorizationDecisionsWorkspace.tsx", from: "setActionError(reason instanceof Error", to: "void (reason instanceof Error", test: dec },
  { id: "E2", file: dec + "AuthorizationDecisionsWorkspace.tsx", from: "if (busy) return;\n    setBusy(true);", to: "setBusy(true);", test: dec },
  { id: "E3", file: dec + "AuthorizationDecisionsWorkspace.tsx", from: "disabled={busy} onClick={() => void run(() => onApprove", to: "onClick={() => void run(() => onApprove", test: dec },
  { id: "T1", file: dec + "models.ts", from: 'timeZone: "Europe/Istanbul"', to: 'timeZone: "UTC"', test: dec },
  { id: "R1", file: dec + "models.ts", from: 'ManagerDiscretion: "Yönetici takdiri",', to: "", test: dec },
  { id: "S1", file: staff + "StaffSection.tsx", from: "found.active ? setConfirmingDeactivate(true) : void toggleActive(found)", to: "void toggleActive(found)", test: staff },
  { id: "S2", file: staff + "StaffSection.tsx", from: "await api.lookupUser(lookedUp)", to: 'await api.lookupUser(field("search").trim())', test: staff },
];
const log = [];
for (const m of mutants) {
  const full = `${root}/${m.file}`;
  const orig = fs.readFileSync(full, "utf8");
  if (!orig.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); continue; }
  fs.writeFileSync(full, orig.replace(m.from, m.to));
  let out = "";
  try { out = execSync(`npx vitest run ${m.test}`, { cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 300000 }); }
  catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const same = fs.readFileSync(full, "utf8") === orig;
  log.push(`${m.id} ${/FAILED|failed/.test(out) ? "KILLED" : "SURVIVED"} restored=${same}`);
  console.log(log[log.length - 1]);
}
fs.writeFileSync("D:/PROJECT/ALKAROS/evidence/V1-RMD-669/mutations.log", log.join("\n") + "\n");
