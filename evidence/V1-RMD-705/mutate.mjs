import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const dir = `${root}/src/Clients/PosTerminal/src/features/catalog`;
const ui = `${dir}/CatalogWorkspace.tsx`;
const api = `${dir}/catalogApi.ts`;
const log = `${root}/evidence/V1-RMD-705/mutations.log`;
const cmd = `cd src/Clients/PosTerminal && export PATH="$PATH:/c/Program Files/nodejs" && npx vitest run src/features/catalog`;

const mutants = [
  { id: "M1", file: api, from: ', list("/product-modifier-assignments"),', to: ', Promise.resolve([]),', note: "assignments never loaded" },
  { id: "M2", file: api, from: 'assignments: "/product-modifier-assignments" }', to: 'assignments: "/modifiers" }', note: "assignment posted to wrong resource" },
  { id: "M3", file: ui, from: "data.modifierGroups.filter((group) => assignedGroupIds.includes(group.id))", to: "data.modifierGroups.filter((group) => !assignedGroupIds.includes(group.id))", note: "wrong groups listed as bound" },
  { id: "M4", file: ui, from: "group.active && !assignedGroupIds.includes(group.id)", to: "group.active", note: "bound group offered again" },
  { id: "M5", file: ui, from: "canAssign={canManage && !!onCreate}", to: "canAssign={true}", note: "assign control shown without permission" },
  { id: "M6", file: ui, from: "productId: product.id, modifierGroupId: assignGroupId", to: "productId: product.id, modifierGroupId: product.id", note: "wrong group sent" },
];

for (const m of mutants) {
  const original = readFileSync(m.file, "utf8");
  if (!original.includes(m.from)) { appendFileSync(log, `${m.id} NOT-APPLIED ${m.note}\n`); continue; }
  writeFileSync(m.file, original.replace(m.from, m.to));
  let result;
  try { execSync(cmd, { cwd: root, shell: "bash", stdio: "pipe", timeout: 300000 }); result = "SURVIVED"; }
  catch { result = "KILLED"; }
  finally { writeFileSync(m.file, original); }
  appendFileSync(log, `${m.id} ${result} restored=${readFileSync(m.file, "utf8") === original} ${m.note}\n`);
}
