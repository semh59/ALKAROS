import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const client = "src/Clients/PosTerminal";
const pc = `${client}/src/features/pending-checks`;
const workspace = `${pc}/PendingChecksWorkspace.tsx`;
const hook = `${pc}/usePendingChecksAlert.ts`;
const pcCss = `${pc}/pending-checks.css`;
const styles = `${client}/src/styles.css`;
const kitchenCss = `${client}/src/features/kitchen-operations/kitchen-operations.css`;
const program = "src/Host/Program.cs";
const order = "database/MigrationComposition/order.json";
const perms = "src/Modules/Identity/Authorization/Catalog/ApplicationPermissions.cs";
const hostTests = "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj";
const idTests = "tests/Modules/Identity/Authorization/ALKAROS.Identity.Authorization.Tests.csproj";

const suites = {
  pending: { kind: "vitest", files: "src/features/pending-checks" },
  touch: { kind: "vitest", files: "src/touchTargets.test.ts" },
  usage: { kind: "dotnet", project: hostTests, filter: "FullyQualifiedName~TheUsageTextNamesEveryVerb" },
  range: { kind: "dotnet", project: hostTests, filter: "FullyQualifiedName~TheDeclaredPhaseBRangeMatches" },
  perms: { kind: "dotnet", project: idTests, filter: "FullyQualifiedName~EveryMigrationSeededCodeIsSeeded" },
};

const mutants = [
  { id: "C1", suite: "pending", file: workspace, from: "if (hasNewCheck(knownIds.current, loaded)) playNewItemChime();", to: "" },
  { id: "C2", suite: "pending", file: hook, from: "return known !== null && checks.some(", to: "return checks.some(" },
  { id: "C3", suite: "pending", file: workspace, from: "        if (disposed) return;\n        setLiveLinkUp(false);\n", to: "        if (disposed) return;\n" },
  { id: "C4", suite: "pending", file: workspace, from: "{error && checks !== null && loadedAt && <p", to: "{false && error && checks !== null && loadedAt && <p" },
  { id: "C5", suite: "pending", file: hook, from: "        setCount(checks.length);\n", to: "" },
  { id: "C6", suite: "pending", file: workspace, from: "knownIds.current = new Set(loaded.map((check) => check.orderId));\n", to: "" },
  { id: "T1", suite: "touch", file: pcCss, from: "min-height: 3rem;", to: "min-height: 2.5rem;" },
  { id: "T2", suite: "touch", file: styles, from: ".quantity-controls button { min-width: 48px; height: 48px;", to: ".quantity-controls button { min-width: 48px; height: 44px;" },
  { id: "T3", suite: "touch", file: kitchenCss, from: "margin-left: auto; min-height: 48px;", to: "margin-left: auto; min-height: 36px;" },
  { id: "T4", suite: "touch", file: styles, from: ".dialog-close { min-width: 48px; min-height: 48px;", to: ".dialog-close { min-width: 44px; min-height: 48px;" },
  { id: "S1", suite: "usage", file: program, from: "        writer.WriteLine(\"       | watchdog --health-url", to: "        writer.WriteLine(\"       | wd --url" },
  { id: "S2", suite: "usage", file: program, from: "audit-disposal --db-url <url> [--apply] [--operator <name>]", to: "audit-disposal --db-url <url> [--apply]" },
  { id: "S3", suite: "range", file: order, from: "\"max\": \"201\"", to: "\"max\": \"139\"" },
  { id: "S4", suite: "perms", file: perms, from: "\"kitchen.reprint\", \"kitchen.availability.suspend\"", to: "\"kitchen.reprint\", \"kitchen.not-seeded\"" },
  { id: "S5", suite: "perms", file: perms, from: "\"identity.roles.manage\", SettingsManage,", to: "\"identity.roles.manage\", OrdersCreate," },
];

const only = process.argv[2] ? process.argv[2].split(",") : null;
const log = [];
for (const m of mutants) {
  if (only && !only.includes(m.id)) continue;
  const full = path.join(root, m.file);
  const orig = fs.readFileSync(full, "utf8");
  const crlf = orig.includes("\r\n");
  const normalized = crlf ? orig.replace(/\r\n/g, "\n") : orig;
  if (!normalized.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); console.log(log.at(-1)); continue; }
  fs.writeFileSync(full, crlf ? normalized.replace(m.from, () => m.to).replace(/\n/g, "\r\n") : normalized.replace(m.from, () => m.to));
  const suite = suites[m.suite];
  let out;
  try {
    const command = suite.kind === "vitest"
      ? `npx vitest run ${suite.files}`
      : `"${process.env.USERPROFILE}/.dotnet/dotnet.exe" test ${suite.project} --filter "${suite.filter}"`;
    out = execSync(command, {
      cwd: suite.kind === "vitest" ? path.join(root, client) : root,
      encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 550000,
      env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" },
    });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const verdict = /error CS|error CA|error TS/.test(out) ? "BUILD-ERROR" : /Başarısız!|FAILED/.test(out) ? "KILLED" : "SURVIVED";
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-699/mutations.log"), log.join("\n") + "\n");
