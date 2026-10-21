import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const dotnet = path.join(os.homedir(), ".dotnet", "dotnet.exe");
const env = { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" };
const draft = "tests/Host/Experience/Orders/TableDraft/ALKAROS.Host.Experience.Orders.TableDraft.Tests.csproj";
const pending = "tests/Host/Experience/PendingOrderNotifications/ALKAROS.Host.Experience.PendingOrderNotifications.Tests.csproj";
const resolver = "src/Host/Experience/Orders/SuggestedWaiterResolver.cs";
const endpoints = "src/Host/Experience/Orders/OrderManagementEndpoints.cs";
const only = process.argv[2] ? process.argv[2].split(",") : null;
const mutants = [
  { id: "L1", file: endpoints, from: "cancellationToken) is { } tableLimit)", to: "cancellationToken) is { } tableLimit && tableLimit < 0)", proj: draft, filter: "FullyQualifiedName~TheFourthTable" },
  { id: "L2", file: resolver, from: "reader.GetInt64(1) >= limit", to: "reader.GetInt64(1) > limit", proj: draft, filter: "FullyQualifiedName~TheFourthTable" },
  { id: "L3", file: resolver, from: "if (reader.GetBoolean(0))", to: "if (!reader.GetBoolean(0) && reader.GetBoolean(0))", proj: draft, filter: "FullyQualifiedName~AnotherRoundOnATable" },
  { id: "L4", file: resolver, from: "WHERE serving_user_id = @user_id AND status NOT IN ('Completed', 'Cancelled', 'Rejected')", to: "WHERE serving_user_id = @user_id AND status NOT IN ('Served', 'Completed', 'Cancelled', 'Rejected')", proj: draft, filter: "FullyQualifiedName~AServedButUnpaid" },
  { id: "L5", file: resolver, from: "if (limit <= 0)", to: "if (limit < 0)", proj: draft, filter: "FullyQualifiedName~WithoutALimit" },
  { id: "L6", file: resolver, from: "COALESCE(w.open_tables, 0) < @max_active_tables", to: "COALESCE(w.active_load, 0) < @max_active_tables", proj: pending, filter: "FullyQualifiedName~SeveralOrdersOnOneTable" },
  { id: "L7", file: resolver, from: "COUNT(DISTINCT COALESCE(table_id, order_id)) FROM orders.orders", to: "COUNT(*) FROM orders.orders", proj: pending, filter: "FullyQualifiedName~SeveralOrdersOnOneTable" },
];
function run(cmd) {
  try { return execSync(cmd, { cwd: root, env, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], maxBuffer: 1 << 28, timeout: 280000 }); }
  catch (e) { return (e.stdout ?? "") + (e.stderr ?? "") + "\nEXEC-ERROR"; }
}
const log = [];
for (const m of mutants) {
  if (only && !only.includes(m.id)) continue;
  const full = path.join(root, m.file);
  const orig = fs.readFileSync(full, "utf8");
  const crlf = orig.includes("\r\n");
  const normalized = crlf ? orig.replace(/\r\n/g, "\n") : orig;
  if (!normalized.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); console.log(log.at(-1)); continue; }
  const mutated = normalized.replace(m.from, () => m.to);
  fs.writeFileSync(full, crlf ? mutated.replace(/\n/g, "\r\n") : mutated);
  let verdict;
  try {
    const build = run(`"${dotnet}" build ${m.proj} -v q`);
    if (/EXEC-ERROR/.test(build)) verdict = "BUILD-ERROR";
    else {
      const out = run(`"${dotnet}" test ${m.proj} --no-build --filter "${m.filter}"`);
      run("taskkill /F /IM testhost.exe");
      verdict = /Başarısız!/.test(out) ? "KILLED" : /Başarılı!/.test(out) ? "SURVIVED" : "INFRA";
    }
  } finally {
    fs.writeFileSync(full, orig);
  }
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-682/mutations.log"), log.join("\n") + "\n");
