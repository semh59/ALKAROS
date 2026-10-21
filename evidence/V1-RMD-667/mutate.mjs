import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const dotnet = path.join(os.homedir(), ".dotnet", "dotnet.exe");
const env = { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" };
const only = process.argv.slice(2);

const hostProject = "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj";
const authProject = "tests/Modules/Identity/Authentication/ALKAROS.Identity.Authentication.Tests.csproj";
const adminProject = "tests/Host/Experience/SecurityAdministration/ALKAROS.Host.Experience.SecurityAdministration.Tests.csproj";

const mutants = [
  { id: "M1", file: "src/Host/DualScreen/DualScreenApplication.Endpoints.cs", from: "await sessions.RevokeDeviceAsync(principal.UserId, $\"cashier:{terminalId:D}\", cancellationToken);\n                context.Response.Cookies.Delete(CashierCookieName);", to: "context.Response.Cookies.Delete(CashierCookieName);", project: hostProject, filter: "FullyQualifiedName~WrongPinsThatLockTheDevice" },
  { id: "M2", file: "src/Modules/Identity/Authentication/AuthenticationService.cs", from: "await _store.RecordLoginFailureAsync(userId, now, _maxFailedAttempts, _lockoutDuration, cancellationToken);\n            return false;", to: "return false;", project: authProject, filter: "FullyQualifiedName~WrongPasswordsOnThePinEndpoint" },
  { id: "M3", file: "src/Modules/Identity/Authentication/PostgresUserStore.cs", from: "                pin_failed_attempts = 0,\n                pin_locked_until = NULL,\n                updated_at = now(),\n                row_version = row_version + 1\n            WHERE user_id = @user_id;\n            \"\"\");\n        command.Parameters.AddWithValue(\"user_id\", userId);\n\n        var affected = await command.ExecuteNonQueryAsync(cancellationToken);\n        return affected == 1;\n    }\n\n    public async Task<bool> SetActiveAsync", to: "                updated_at = now(),\n                row_version = row_version + 1\n            WHERE user_id = @user_id;\n            \"\"\");\n        command.Parameters.AddWithValue(\"user_id\", userId);\n\n        var affected = await command.ExecuteNonQueryAsync(cancellationToken);\n        return affected == 1;\n    }\n\n    public async Task<bool> SetActiveAsync", project: authProject, filter: "FullyQualifiedName~ForceUnlockAlsoClearsThePinLock" },
  { id: "M4", file: "src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs", from: "\n                        || (user.PinLockedUntil ?? DateTimeOffset.MinValue) > DateTimeOffset.UtcNow));", to: "));", project: adminProject, filter: "FullyQualifiedName~LookupShowsAPinOnlyLock" },
  { id: "M5", file: "src/Host/DualScreen/DualScreenApplication.Endpoints.cs", from: "pin.Length is < 4 or > 12", to: "pin.Length is < 4 or > 13", project: hostProject, filter: "FullyQualifiedName~PinsMustBeFourToTwelve" },
  { id: "M6", file: "src/Host/DualScreen/DualScreenApplication.Endpoints.cs", from: "steps.All(s => s == 1) || ", to: "", project: hostProject, filter: "FullyQualifiedName~PinsMustBeFourToTwelve" },
  { id: "M7", file: "src/Host/DualScreen/DualScreenApplication.Endpoints.cs", from: "pinSet = (await users.GetByIdAsync(principal.UserId, cancellationToken))?.PinHash is not null,", to: "pinSet = false,", project: hostProject, filter: "FullyQualifiedName~LoginAndSessionReportWhetherAPinIsSet" },
  { id: "M8", file: "src/Modules/Identity/Authentication/AuthenticationService.cs", from: "if (user is null || !user.Active || (user.LockedUntil is { } lockedUntil && lockedUntil > now))\n            return false;", to: "if (user is null || !user.Active)\n            return false;", project: authProject, filter: "FullyQualifiedName~WrongPasswordsOnThePinEndpoint" },
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
  const orig = path.join(os.tmpdir(), `m667-${m.id}.orig`);
  fs.copyFileSync(full, orig);
  let text = fs.readFileSync(full, "utf8");
  if (!text.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); continue; }
  fs.writeFileSync(full, text.replace(m.from, m.to));
  let verdict;
  try {
    const build = run(`"${dotnet}" build ${m.project} -v q`);
    if (/EXEC-ERROR/.test(build) && !/Hata\s+0|0 Hata/.test(build)) { verdict = "BUILD-ERROR"; }
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
fs.writeFileSync(path.join(root, "evidence/V1-RMD-667/mutations.log"), log.join("\n") + "\n", { flag: only.length ? "a" : "w" });
