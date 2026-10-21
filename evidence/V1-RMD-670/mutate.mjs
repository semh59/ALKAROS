import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const dotnet = path.join(os.homedir(), ".dotnet", "dotnet.exe");
const env = { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" };
const only = process.argv.slice(2);

const voidProject = "tests/Host/Experience/Orders/VoidSent/ALKAROS.Host.Experience.Orders.VoidSent.Tests.csproj";
const billingProject = "tests/Host/Experience/Billing/ALKAROS.Host.Experience.Billing.Tests.csproj";
const decisionProject = "tests/Host/Experience/Authorization/ALKAROS.Host.Experience.Authorization.Tests.csproj";
const offlineProject = "tests/Host/Experience/OfflineReconciliation/ALKAROS.Host.Experience.OfflineReconciliation.Tests.csproj";
const authzProject = "tests/Modules/Identity/Authorization/ALKAROS.Identity.Authorization.Tests.csproj";

const mutants = [
  { id: "V1", file: "src/Host/Experience/Orders/OrderManagementEndpoints.cs", from: "voidedItem.GrossAmount,", to: "0m,", project: voidProject, filter: "FullyQualifiedName~APendingVoidRequestCarriesTheItemsRealAmount" },
  { id: "D1", file: "src/Host/Experience/Billing/BillingSplitApplication.cs", from: "var grantAmount = string.Equals(request.CalculationType, \"Percentage\", StringComparison.Ordinal)", to: "var grantAmount = false", project: billingProject, filter: "FullyQualifiedName~APercentageDiscountRequestIsRaised" },
  { id: "D2", file: "src/Host/Experience/Billing/BillingSplitApplication.cs", from: ".Summary.OriginalPayableAmount * request.Value / 100m)", to: ".Summary.OriginalSubtotal * request.Value / 100m)", project: billingProject, filter: "FullyQualifiedName~APercentageDiscountRequestIsRaised" },
  { id: "P1", file: "src/Host/Experience/Authorization/AuthorizationDecisionStore.cs", from: "await _authorization.AuthorizeAsync(approverUserId, pending.PermissionCode, cancellationToken);", to: "await Task.CompletedTask;", project: decisionProject, filter: "FullyQualifiedName~AnApproverWhoCannotDo" },
  { id: "P2", file: "src/Host/Composition/Errors/ApiErrorCatalog.cs", from: "ApproverLacksPermissionException =>", to: "ApproverLacksPermissionException when false =>", project: decisionProject, filter: "FullyQualifiedName~AnApproverWhoCannotDo" },
  { id: "T1", file: "src/Modules/Identity/Authorization/Grants/AuthorizationGrantService.cs", from: "if (forceEscalation)\n            return await StoreAsync(request, GrantStatus.Pending", to: "if (forceEscalation && request.Amount < -1m)\n            return await StoreAsync(request, GrantStatus.Pending", project: authzProject, filter: "FullyQualifiedName~AuthorizationGrantServiceTests" },
  { id: "O1", file: "src/Modules/Identity/Authorization/Offline/OfflineGrantReconciler.cs", from: "if (!MatchesReplay(replay, action))", to: "if (false)", project: offlineProject, filter: "FullyQualifiedName~ResendingAKeyWithAnotherAmount" },
  { id: "O2", file: "src/Modules/Identity/Authorization/Offline/OfflineGrantReconciler.cs", from: "&& replay.Amount == action.Amount", to: "&& true", project: offlineProject, filter: "FullyQualifiedName~ResendingAKeyWithAnotherAmount" },
  { id: "O3", file: "src/Host/Experience/OfflineReconciliation/OfflineReconciliationEndpoints.cs", from: "IdempotencyKeyReusedException =>", to: "IdempotencyKeyReusedException when false =>", project: offlineProject, filter: "FullyQualifiedName~ResendingAKeyWithAnotherAmount" },
];

function run(cmd) {
  try {
    return execSync(cmd, { cwd: root, env, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], maxBuffer: 1 << 28, timeout: 540000 });
  } catch (e) {
    return (e.stdout ?? "") + (e.stderr ?? "") + "\nEXEC-ERROR";
  }
}

const log = [];
for (const m of mutants) {
  if (only.length && !only.includes(m.id)) continue;
  const full = path.join(root, m.file);
  const orig = path.join(os.tmpdir(), `m670-${m.id}.orig`);
  fs.copyFileSync(full, orig);
  const text = fs.readFileSync(full, "utf8");
  const crlf = text.includes("\r\n");
  const normalized = crlf ? text.replace(/\r\n/g, "\n") : text;
  if (!normalized.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); console.log(log[log.length - 1]); continue; }
  let mutated = normalized.replace(m.from, m.to);
  if (crlf) mutated = mutated.replace(/\n/g, "\r\n");
  fs.writeFileSync(full, mutated);
  let verdict;
  try {
    const build = run(`"${dotnet}" build ${m.project} -v q`);
    if (/EXEC-ERROR/.test(build) && !/Hata\s+0|0 Hata/.test(build)) verdict = "BUILD-ERROR";
    else {
      const out = run(`"${dotnet}" test ${m.project} --no-build --filter "${m.filter}"`);
      run("taskkill /F /IM testhost.exe");
      if (/Başarısız!/.test(out)) verdict = "KILLED";
      else if (/Başarılı!/.test(out)) verdict = "SURVIVED";
      else verdict = "INFRA";
    }
  } finally {
    fs.copyFileSync(orig, full);
  }
  const same = fs.readFileSync(full, "utf8") === fs.readFileSync(orig, "utf8");
  log.push(`${m.id} ${verdict} restored=${same} (${m.file})`);
  console.log(log[log.length - 1]);
}
fs.writeFileSync(path.join(root, "evidence/V1-RMD-670/mutations.log"), log.join("\n") + "\n", { flag: "a" });
