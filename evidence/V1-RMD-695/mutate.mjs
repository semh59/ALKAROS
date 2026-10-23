import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const hostComposition = "src/Host/Composition/HostComposition.cs";
const app = "src/Host/DualScreen/DualScreenApplication.cs";
const orders = "src/Host/DualScreen/DualScreenStore.Orders.cs";
const display = "src/Host/DualScreen/DualScreenStore.Display.cs";
const helpStore = "src/Host/Experience/HelpRequests/HelpRequestStore.cs";
const endpoints = "src/Host/DualScreen/DualScreenApplication.Endpoints.cs";
const archTest = "tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs";
const suites = {
  url: { project: "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj", filter: "FullyQualifiedName~DatabaseUrlTests|FullyQualifiedName~StartupRequirementTests|FullyQualifiedName~OrderNumberTests" },
  st: { project: "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj", filter: "FullyQualifiedName~DualScreenStoreTests" },
  ds: { project: "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj", filter: "FullyQualifiedName~DualScreenAuthorizationHttpTests" },
  help: { project: "tests/Host/Experience/HelpRequests/ALKAROS.Host.Experience.HelpRequests.Tests.csproj", filter: null },
  arch: { project: "tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj", filter: null },
};
const mutants = [
  { id: "U1", suite: "url", file: hostComposition, from: "Username = !string.IsNullOrEmpty(uri.UserInfo) ? Uri.UnescapeDataString(uri.UserInfo.Split(':')[0])", to: "Username = !string.IsNullOrEmpty(uri.UserInfo) ? uri.UserInfo.Split(':')[0]" },
  { id: "U2", suite: "url", file: hostComposition, from: "Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))", to: "uri.AbsolutePath.TrimStart('/')" },
  { id: "K1", suite: "url", file: app, from: "if (string.IsNullOrWhiteSpace(stationId))\n        {\n            throw new DualScreenStartupException", to: "if (stationId is null)\n        {\n            throw new DualScreenStartupException" },
  { id: "N1", suite: "url", file: orders, from: "TimeZoneInfo.ConvertTime(utcNow, RestaurantTimeZone):yyyyMMdd-HHmmss", to: "utcNow:yyyyMMdd-HHmmss" },
  { id: "H1", suite: "help", file: helpStore, from: "TimeSpan.FromSeconds(elapsedSeconds)", to: "TimeSpan.FromMilliseconds(elapsedSeconds)" },
  { id: "P1", suite: "st", file: display, from: "expires_at <= now(), approved_at", to: "expires_at > now(), approved_at" },
  { id: "P2", suite: "st", file: display, from: "now() + @lifetime, NULL, NULL, 0, now())", to: "now() - @lifetime, NULL, NULL, 0, now())" },
  { id: "P3", suite: "st", file: display, from: "VALUES (@session_id, @display_id, @terminal_id, @token_hash, now(), now() + @lifetime, NULL, now())", to: "VALUES (@session_id, @display_id, @terminal_id, @token_hash, now(), now() - @lifetime, NULL, now())" },
  { id: "D1", suite: "ds", file: endpoints, from: "roleName = governingRole?.Name,", to: "roleName = (string?)null," },
  { id: "A1", suite: "arch", file: archTest, from: "[\"ALKAROS.Host.Experience.Menu\"] =\n                [\"ALKAROS.Identity\", \"ALKAROS.Menu\"],", to: "[\"ALKAROS.Host.Experience.Menu\"] =\n                [\"ALKAROS.Identity\"]," },
  { id: "A2", suite: "arch", file: archTest, from: "[\"ALKAROS.Host.Experience.WebPush\"] =\n                [\"ALKAROS.Secrets\"],\n", to: "" },
  { id: "A3", suite: "arch", file: archTest, from: "[\"ALKAROS.Host.Experience.OnlineOrdering\"] =\n                [\"ALKAROS.Orders\", \"ALKAROS.Kitchen\", \"ALKAROS.Catalog\", \"ALKAROS.Identity\", \"ALKAROS.Audit\", \"ALKAROS.Inventory\", \"ALKAROS.OnlineOrdering\", \"ALKAROS.Invoicing\", \"ALKAROS.Secrets\"],", to: "[\"ALKAROS.Host.Experience.OnlineOrdering\"] =\n                [\"ALKAROS.Orders\", \"ALKAROS.Kitchen\", \"ALKAROS.Catalog\", \"ALKAROS.Identity\", \"ALKAROS.Audit\", \"ALKAROS.Inventory\", \"ALKAROS.OnlineOrdering\", \"ALKAROS.Invoicing\"]," },
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
  let out;
  try {
    const suite = suites[m.suite];
    const filter = suite.filter ? ` --filter "${suite.filter}"` : "";
    out = execSync(`"${process.env.USERPROFILE}/.dotnet/dotnet.exe" test ${suite.project}${filter}`, {
      cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 550000,
      env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" },
    });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const verdict = /error CS|error CA/.test(out) ? "BUILD-ERROR" : /Başarısız!|FAILED/.test(out) ? "KILLED" : "SURVIVED";
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-695/mutations.log"), log.join("\n") + "\n");
