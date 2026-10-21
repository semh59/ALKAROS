import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const dotnet = path.join(os.homedir(), ".dotnet", "dotnet.exe");
const env = { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" };
const only = process.argv.slice(2);

const authz = "tests/Modules/Identity/Authorization/ALKAROS.Identity.Authorization.Tests.csproj";
const host = "tests/Host/Experience/OfflineReconciliation/ALKAROS.Host.Experience.OfflineReconciliation.Tests.csproj";
const off = "src/Modules/Identity/Authorization/Offline/";
const wwwroot = "src/Clients/WaiterPwa/wwwroot/";

const mutants = [
  { id: "L1", file: off + "OfflineGrantReconciler.cs", from: "await using var budgetLock = await _replays.LockBudgetAsync(budgetId, cancellationToken);", to: "await Task.CompletedTask;", project: authz, filter: "FullyQualifiedName~ParallelReconnectsOfOneBudget" },
  { id: "C1", file: off + "PostgresOfflineAuthorityBudgetRepository.cs", from: "line with { MaxCount = Math.Max(0, line.MaxCount - spent.GetValueOrDefault(line.PermissionCode)) }", to: "line", project: authz, filter: "FullyQualifiedName~AFreshBudgetForTheSameUser" },
  { id: "T1", file: off + "OfflineGrantReconciler.cs", from: "if (action.OfflineAuthorizedAt > _nowUtc() + ClockSkew)", to: "if (action.OfflineAuthorizedAt > _nowUtc() + ClockSkew && action.Amount < -1m)", project: authz, filter: "FullyQualifiedName~FromTheFutureOrBeforeTheBudget" },
  { id: "T2", file: off + "OfflineGrantReconciler.cs", from: "if (action.OfflineAuthorizedAt < budget.IssuedAt)", to: "if (action.OfflineAuthorizedAt < budget.IssuedAt && action.Amount < -1m)", project: authz, filter: "FullyQualifiedName~FromTheFutureOrBeforeTheBudget" },
  { id: "V1", file: off + "OfflineGrantReconciler.cs", from: "        foreach (var action in actions)\n            action.Validate();\n", to: "        foreach (var action in actions)\n            _ = action.Amount;\n", project: authz, filter: "FullyQualifiedName~AMalformedActionRefusesTheBatch" },
  { id: "B1", file: "src/Host/Experience/OfflineReconciliation/OfflineReconciliationEndpoints.cs", from: "request.Actions.Count > MaxActionsPerBatch", to: "request.Actions.Count > 5000", project: host, filter: "FullyQualifiedName~ABatchOverTheLimit" },
  { id: "E1", file: "src/Host/Experience/OfflineReconciliation/OfflineReconciliationEndpoints.cs", from: "new ApiErrorEnvelope(new ApiError(code, message, status, invocationContext.HttpContext.TraceIdentifier))", to: "new { code, message, status }", project: host, filter: "FullyQualifiedName~Offline" },
  { id: "J1", file: wwwroot + "js/offline-queue.js", from: "status >= 500;", to: "status >= 600;", client: true },
  { id: "J2", file: wwwroot + "js/offline-queue.js", from: "Boolean(result.offline) || ", to: "", client: true },
  { id: "J3", file: wwwroot + "js/offline-queue.js", from: "status === 409 || status === 423", to: "status === 423", client: true },
  { id: "J4", file: wwwroot + "js/offline-queue.js", from: "state.offlineActions.slice(0, MAX_RECONCILE_BATCH)", to: "state.offlineActions.slice()", client: true },
  { id: "J5", file: wwwroot + "js/state.js", from: "if (previous && previous !== userId) {", to: "if (previous && previous !== userId && false) {", client: true },
];

function run(cmd, cwd = root) {
  try {
    return execSync(cmd, { cwd, env, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], maxBuffer: 1 << 28, timeout: 540000 });
  } catch (e) {
    return (e.stdout ?? "") + (e.stderr ?? "") + "\nEXEC-ERROR";
  }
}

const log = [];
for (const m of mutants) {
  if (only.length && !only.includes(m.id)) continue;
  const full = path.join(root, m.file);
  const orig = path.join(os.tmpdir(), `m671-${m.id}.orig`);
  fs.copyFileSync(full, orig);
  const text = fs.readFileSync(full, "utf8");
  const crlf = text.includes("\r\n");
  const normalized = crlf ? text.replace(/\r\n/g, "\n") : text;
  if (!normalized.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); console.log(log[log.length - 1]); continue; }
  let mutated = normalized.replace(m.from, () => m.to);
  if (crlf) mutated = mutated.replace(/\n/g, "\r\n");
  fs.writeFileSync(full, mutated);
  let verdict;
  try {
    if (m.client) {
      const out = run("npx vitest run offline-queue.test.js", path.join(root, "tests/Clients/StaticApps"));
      verdict = /failed|FAILED/.test(out) ? "KILLED" : /passed/.test(out) ? "SURVIVED" : "INFRA";
    } else {
      const build = run(`"${dotnet}" build ${m.project} -v q`);
      if (/EXEC-ERROR/.test(build)) verdict = "BUILD-ERROR";
      else {
        const out = run(`"${dotnet}" test ${m.project} --no-build --filter "${m.filter}"`);
        run("taskkill /F /IM testhost.exe");
        verdict = /Başarısız!/.test(out) ? "KILLED" : /Başarılı!/.test(out) ? "SURVIVED" : "INFRA";
      }
    }
  } finally {
    fs.copyFileSync(orig, full);
  }
  const same = fs.readFileSync(full, "utf8") === fs.readFileSync(orig, "utf8");
  log.push(`${m.id} ${verdict} restored=${same} (${m.file})`);
  console.log(log[log.length - 1]);
}
fs.writeFileSync(path.join(root, "evidence/V1-RMD-671/mutations.log"), log.join("\n") + "\n", { flag: "a" });
