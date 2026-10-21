import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execSync } from "node:child_process";
const root = "D:/PROJECT/ALKAROS";
const dotnet = path.join(os.homedir(), ".dotnet", "dotnet.exe");
const env = { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" };
const proj = "tests/Host/Experience/Settings/ALKAROS.Host.Experience.Settings.Tests.csproj";
const ep = "src/Host/Experience/Settings/SettingsManagementEndpoints.cs";
const mutants = [
  { id: "S1", from: "ChangedByName = entry.ChangedBy is { } id ? names.GetValueOrDefault(id) : null,", to: "ChangedByName = null," },
  { id: "S2", from: "            await EnsureKnownSettingsRegisteredAsync(settings, cancellationToken);\n", to: "" },
  { id: "S3", from: "await WaiterMaxActiveTablesSetting.EnsureRegisteredAsync(settings, cancellationToken);\n        await GarsonFeatureToggles", to: "await GarsonFeatureToggles" },
];
const run = (cmd) => { try { return execSync(cmd, { cwd: root, env, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], maxBuffer: 1 << 28, timeout: 280000 }); } catch (e) { return (e.stdout ?? "") + (e.stderr ?? "") + "\nEXEC-ERROR"; } };
const log = [];
for (const m of mutants) {
  const full = path.join(root, ep);
  const orig = fs.readFileSync(full, "utf8");
  const crlf = orig.includes("\r\n");
  const norm = crlf ? orig.replace(/\r\n/g, "\n") : orig;
  if (!norm.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); console.log(log.at(-1)); continue; }
  const mutated = norm.replace(m.from, () => m.to);
  fs.writeFileSync(full, crlf ? mutated.replace(/\n/g, "\r\n") : mutated);
  let verdict;
  try {
    const build = run(`"${dotnet}" build ${proj} -v q`);
    if (/EXEC-ERROR/.test(build)) verdict = "BUILD-ERROR";
    else {
      const out = run(`"${dotnet}" test ${proj} --no-build`);
      run("taskkill /F /IM testhost.exe");
      verdict = /Başarısız!/.test(out) ? "KILLED" : /Başarılı!/.test(out) ? "SURVIVED" : "INFRA";
    }
  } finally { fs.writeFileSync(full, orig); }
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-684/mutations.log"), log.join("\n") + "\n");
