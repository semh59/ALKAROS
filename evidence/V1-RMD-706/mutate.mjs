import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const file = `${root}/src/Host/Experience/Catalog/CatalogManagementEndpoints.cs`;
const log = `${root}/evidence/V1-RMD-706/mutations.log`;
const cmd = "ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres ~/.dotnet/dotnet.exe test tests/Host/Experience/Catalog/ALKAROS.Host.Experience.Catalog.Tests.csproj";

const mutants = [
  { id: "A1", from: "await AppendAuditAsync(context.HttpContext, actorId, result);", to: "", note: "no audit event written" },
  { id: "A2", from: "StatusCode: >= 200 and < 300 }", to: "StatusCode: >= 200 }", note: "rejected writes audited too" },
  { id: "A3", from: "                actorId: actorId,\n                afterStateJson", to: "                actorId: null,\n                afterStateJson", note: "actor missing" },
  { id: "A4", from: "var action = segments.Length > 1 ? segments[1] : \"create\";", to: "var action = \"create\";", note: "action name lost" },
  { id: "A5", from: "afterStateJson: value is null ? null : JsonSerializer.Serialize(value)", to: "afterStateJson: null", note: "result snapshot lost" },
  { id: "A6", from: "aggregateType: $\"catalog.{resource}\"", to: "aggregateType: \"catalog\"", note: "aggregate type lost" },
];

const only = process.argv.slice(2);
for (const m of mutants) {
  if (only.length && !only.includes(m.id)) continue;
  const original = readFileSync(file, "utf8");
  const nl = original.includes("\r\n") ? "\r\n" : "\n";
  const from = m.from.replace(/\n/g, nl);
  if (!original.includes(from)) { appendFileSync(log, `${m.id} NOT-APPLIED ${m.note}\n`); continue; }
  writeFileSync(file, original.replace(from, m.to.replace(/\n/g, nl)));
  let result;
  try { execSync(cmd, { cwd: root, shell: "bash", stdio: "pipe", timeout: 600000 }); result = "SURVIVED"; }
  catch (e) { result = /error CS|error CA/.test(`${e.stdout}${e.stderr}`) ? "BUILD-ERROR" : "KILLED"; }
  finally { writeFileSync(file, original); }
  appendFileSync(log, `${m.id} ${result} restored=${readFileSync(file, "utf8") === original} ${m.note}\n`);
}
