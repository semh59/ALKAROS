import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const repo = "src/Modules/Settings/TypedSettings/PostgresSettingsRepository.cs";
const gar = "src/Modules/Settings/GarsonFeatureToggles/GarsonFeatureToggles.cs";
const ep = "src/Host/Experience/Settings/SettingsManagementEndpoints.cs";
const typed = "tests/Modules/Settings/TypedSettings/ALKAROS.Settings.TypedSettings.Tests.csproj";
const garProj = typed;
const mutants = [
  { id: "R1", file: repo, from: "ORDER BY changed_at DESC, setting_history_id DESC", to: "ORDER BY changed_at DESC", proj: typed },
  { id: "R2", file: repo, from: "ORDER BY changed_at DESC, setting_history_id DESC", to: "ORDER BY changed_at DESC, setting_history_id ASC", proj: typed },
  { id: "R3", file: repo, from: "ORDER BY changed_at DESC, setting_history_id DESC\n            LIMIT {MaxUnpagedRows};", to: "ORDER BY changed_at ASC, setting_history_id DESC\n            LIMIT {MaxUnpagedRows};", proj: typed },
  { id: "R4", file: repo, from: "ORDER BY changed_at DESC, setting_history_id DESC\n            LIMIT {MaxUnpagedRows};", to: "ORDER BY changed_at DESC, setting_history_id DESC\n            LIMIT {MaxUnpagedRows - 1};", proj: typed },
  { id: "G1", file: gar, from: "records.All(record => record.Key != definition.Key)", to: "false", proj: garProj },
  { id: "G2", file: gar, from: "record is not { Active: true } ||", to: "record is null ||", proj: garProj },
  { id: "G3", file: gar, from: "|| enabled;", to: "&& enabled;", proj: garProj },
  { id: "G4", file: gar, from: "candidate.Key == definition.Value.Key", to: "true", proj: garProj },
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
  const mutated = normalized.replace(m.from, () => m.to);
  fs.writeFileSync(full, crlf ? mutated.replace(/\n/g, "\r\n") : mutated);
  let out;
  try {
    out = execSync(`"${process.env.USERPROFILE}/.dotnet/dotnet.exe" test ${m.proj}`, {
      cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 500000,
      env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" },
    });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const verdict = /error CS|error CA/.test(out) ? "BUILD-ERROR" : /Başarısız!|FAILED/.test(out) ? "KILLED" : "SURVIVED";
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-686/mutations.log"), log.join("\n") + "\n");
