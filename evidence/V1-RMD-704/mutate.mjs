import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const dir = `${root}/src/Clients/PosTerminal/src`;
const ui = `${dir}/features/catalog/CatalogWorkspace.tsx`;
const api = `${dir}/features/catalog/catalogApi.ts`;
const models = `${dir}/features/catalog/models.ts`;
const route = `${dir}/routes/workspace.tsx`;
const log = `${root}/evidence/V1-RMD-704/mutations.log`;
const cmd = `cd src/Clients/PosTerminal && export PATH="$PATH:/c/Program Files/nodejs" && npx vitest run src/features/catalog src/routes/workspace.test.tsx`;

const mutants = [
  { id: "M1", file: api, from: "if (!result.nextCursor) return items;", to: "return items;", note: "only first page loaded" },
  { id: "M2", file: api, from: "const MaximumPages = 100;", to: "const MaximumPages = 1;", note: "page cap far too low" },
  { id: "M3", file: api, from: "if (response.status === 401) notifySessionExpired();", to: "", note: "401 not reported" },
  { id: "M4", file: api, from: "cursor ? `limit=100&cursor=${encodeURIComponent(cursor)}`", to: "cursor ? `limit=100&cursor=${cursor}`", note: "cursor not encoded" },
  { id: "M5", file: ui, from: "reason.status === 409;", to: "/409|conflict|overlap|duplicate/i.test(message);", note: "conflict by message text" },
  { id: "M6", file: ui, from: "if (submitting) return;", to: "", note: "double submit allowed" },
  { id: "M7", file: ui, from: "toCreateInput(kind, draft, draftId)", to: "toCreateInput(kind, draft, crypto.randomUUID())", note: "new id per submit" },
  { id: "M8", file: ui, from: "new Date(date.getTime() - date.getTimezoneOffset() * 60_000)", to: "new Date(date.getTime())", note: "UTC default" },
  { id: "M9", file: ui, from: "setFormErrors([conflict ?", to: "setFeedback({ tone: \"error\", message: conflict ?", note: "error outside dialog" },
  { id: "M10", file: models, from: "items.length === 0)", to: "items.length >= 0)", note: "everything counts as empty" },
  { id: "M11", file: route, from: "isCatalogEmpty(loaded) ? \"empty\" : \"ready\"", to: "\"ready\"", note: "empty state never set" },
];

const only = process.argv.slice(2);
for (const m of mutants) {
  if (only.length && !only.includes(m.id)) continue;
  const original = readFileSync(m.file, "utf8");
  if (!original.includes(m.from)) { appendFileSync(log, `${m.id} NOT-APPLIED ${m.note}\n`); continue; }
  writeFileSync(m.file, original.replace(m.from, m.to));
  let result;
  try { execSync(cmd, { cwd: root, shell: "bash", stdio: "pipe", timeout: 300000 }); result = "SURVIVED"; }
  catch { result = "KILLED"; }
  finally { writeFileSync(m.file, original); }
  appendFileSync(log, `${m.id} ${result} restored=${readFileSync(m.file, "utf8") === original} ${m.note}\n`);
}
