import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const store = `${root}/src/Host/Experience/Catalog/CatalogManagementStore.cs`;
const ui = `${root}/src/Clients/PosTerminal/src/features/catalog/CatalogWorkspace.tsx`;
const log = `${root}/evidence/V1-RMD-702/mutations.log`;
const hostTest = `ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres ~/.dotnet/dotnet.exe test tests/Host/Experience/Catalog/ALKAROS.Host.Experience.Catalog.Tests.csproj`;
const uiTest = `cd src/Clients/PosTerminal && export PATH="$PATH:/c/Program Files/nodejs" && npx vitest run src/features/catalog`;

const mutants = [
  { id: "M1", file: store, from: "if (taxProfile is not { Active: true })", to: "if (false)", cmd: hostTest, note: "no profile check" },
  { id: "M2", file: store, from: "if (taxProfile is not { Active: true })", to: "if (taxProfile is null)", cmd: hostTest, note: "inactive profile accepted" },
  { id: "M3", file: ui, from: 'if (kind === "products" && !draft.taxProfileId)', to: 'if (false)', cmd: uiTest, note: "no client validation" },
  { id: "M4", file: ui, from: "taxProfileId: draft.taxProfileId || null", to: "taxProfileId: null", cmd: uiTest, note: "profile not sent" },
  { id: "M5", file: ui, from: "categoryId: draft.categoryId || null", to: "categoryId: null", cmd: uiTest, note: "category not sent" },
];

const only = process.argv.slice(2);
for (const m of mutants) {
  if (only.length && !only.includes(m.id)) continue;
  const original = readFileSync(m.file, "utf8");
  if (!original.includes(m.from)) { appendFileSync(log, `${m.id} NOT-APPLIED ${m.note}\n`); continue; }
  writeFileSync(m.file, original.replace(m.from, m.to));
  let result;
  try {
    execSync(m.cmd, { cwd: root, shell: "bash", stdio: "pipe", timeout: 600000 });
    result = "SURVIVED";
  } catch (e) {
    const out = `${e.stdout}${e.stderr}`;
    result = /error CS|error TS/.test(out) ? "BUILD-ERROR" : "KILLED";
  } finally {
    writeFileSync(m.file, original);
  }
  const restored = readFileSync(m.file, "utf8") === original;
  appendFileSync(log, `${m.id} ${result} restored=${restored} ${m.note}\n`);
}
