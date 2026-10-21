import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const dotnet = path.join(os.homedir(), ".dotnet", "dotnet.exe");
const env = { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" };
const typed = "tests/Modules/Settings/TypedSettings/ALKAROS.Settings.TypedSettings.Tests.csproj";
const host = "tests/Host/Experience/Settings/ALKAROS.Host.Experience.Settings.Tests.csproj";
const val = "src/Modules/Settings/TypedSettings/SettingValidator.cs";
const repo = "src/Modules/Settings/TypedSettings/PostgresSettingsRepository.cs";
const req = "src/Modules/Settings/TypedSettings/SettingRequests.cs";
const ep = "src/Host/Experience/Settings/SettingsManagementEndpoints.cs";
const cat = "src/Host/Composition/Errors/ApiErrorCatalog.cs";
const only = process.argv[2] ? process.argv[2].split(",") : null;
const mutants = [
  { id: "V1", file: val, from: "WholeNumberBetween(v, 0, 50)", to: "WholeNumberBetween(v, 0, 51)", proj: typed },
  { id: "V2", file: val, from: "WholeNumberBetween(v, 1, 99)", to: "WholeNumberBetween(v, 0, 99)", proj: typed },
  { id: "V3", file: val, from: "DurationBetween(v, TimeSpan.FromMinutes(1), TimeSpan.FromHours(24))", to: "DurationBetween(v, TimeSpan.FromSeconds(1), TimeSpan.FromHours(24))", proj: typed },
  { id: "V4", file: val, from: "if (!value.Contains(':') || !TimeSpan", to: "if (!TimeSpan", proj: typed },
  { id: "V5", file: val, from: "return timeSpan > TimeSpan.Zero;", to: "return timeSpan >= TimeSpan.Zero;", proj: typed },
  { id: "V6", file: val, from: "Where(char.IsLetterOrDigit).ToArray()", to: "Where(c => c != '_').ToArray()", proj: typed },
  { id: "V7", file: val, from: '        "pwd",\n', to: "", proj: typed },
  { id: "V8", file: repo, from: "if (!active)\n            throw new SettingInactiveException", to: 'if (!active && request.Key == "")\n            throw new SettingInactiveException', proj: typed },
  { id: "V9", file: repo, from: "if (!reader.GetBoolean(8))\n                throw new SettingInactiveException", to: 'if (!reader.GetBoolean(8) && request.Key == "")\n                throw new SettingInactiveException', proj: typed },
  { id: "V10", file: repo, from: "            ON CONFLICT (setting_key) DO NOTHING\n", to: "", proj: typed },
  { id: "V11", file: req, from: "Length: > MaxLength", to: "Length: > 5000", proj: typed },
  { id: "V12", file: val, from: "value.Length > MaxTextLength", to: "value.Length > 20000", proj: typed },
  { id: "V13", file: val, from: "v.Length <= 80", to: "v.Length <= 81", proj: typed },
  { id: "V14", file: val, from: "KeyRules.TryGetValue(settingKey, out var rule) && !rule.IsValid(value)", to: "KeyRules.TryGetValue(settingKey, out var rule) && false && !rule.IsValid(value)", proj: typed },
  { id: "V15", file: ep, from: "BusinessAccentPalette.All.All(entry => entry.Key != request.NewValue)", to: "BusinessAccentPalette.All.All(entry => entry.Key == request.NewValue)", proj: host },
  { id: "V16", file: cat, from: 'Status409Conflict, "SETTING_INACTIVE"', to: 'Status400BadRequest, "SETTING_INACTIVE"', proj: host },
];
function run(cmd) {
  try { return execSync(cmd, { cwd: root, env, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], maxBuffer: 1 << 28, timeout: 290000 }); }
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
      const out = run(`"${dotnet}" test ${m.proj} --no-build`);
      run("taskkill /F /IM testhost.exe");
      verdict = /Başarısız!/.test(out) ? "KILLED" : /Başarılı!/.test(out) ? "SURVIVED" : "INFRA";
    }
  } finally {
    fs.writeFileSync(full, orig);
  }
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-683/mutations.log"), log.join("\n") + "\n");
