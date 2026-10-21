import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const up = "database/migrations/V1/V1-RMD-687/197-settings-history-immutable.up.sql";
const typed = "tests/Modules/Settings/TypedSettings/ALKAROS.Settings.TypedSettings.Tests.csproj";
const ident = "tests/Modules/Settings/BusinessIdentity/ALKAROS.Settings.BusinessIdentity.Tests.csproj";
const mutants = [
  { id: "M1", from: "ON DELETE RESTRICT", to: "ON DELETE CASCADE", proj: typed },
  { id: "M2", from: "RAISE EXCEPTION 'settings.setting_history rows cannot be deleted.';", to: "NULL;", proj: typed },
  { id: "M3", from: "OR NEW.new_value IS DISTINCT FROM OLD.new_value", to: "", proj: typed },
  { id: "M4", from: "OR NEW.reason IS DISTINCT FROM OLD.reason", to: "", proj: typed },
  { id: "M5", from: "OR NEW.changed_at IS DISTINCT FROM OLD.changed_at", to: "", proj: typed },
  { id: "M6", from: "NEW.setting_history_id IS DISTINCT FROM OLD.setting_history_id\n       OR", to: "", proj: typed },
  { id: "M7", from: "OR NEW.setting_id IS DISTINCT FROM OLD.setting_id", to: "", proj: typed },
  { id: "M8", from: "OR NEW.old_value IS DISTINCT FROM OLD.old_value", to: "", proj: typed },
  { id: "M9", from: "OR NEW.changed_by IS NOT NULL", to: "", proj: typed },
  { id: "M10", from: "BEFORE UPDATE OR DELETE ON", to: "BEFORE DELETE ON", proj: typed },
  { id: "M11", from: "'image/jpeg', ", to: "", proj: ident },
  { id: "M12", from: "BETWEEN 1 AND 5242880", to: "BETWEEN 1 AND 5242881", proj: ident },
  { id: "M13", from: "BETWEEN 1 AND", to: "BETWEEN 0 AND", proj: ident },
  { id: "M14", from: "'image/png', ", to: "", proj: ident },
  { id: "M15", from: "CHECK (content_type IN", to: "CHECK (true OR content_type IN", proj: ident },
];
const only = process.argv[2] ? process.argv[2].split(",") : null;
const log = [];
for (const m of mutants) {
  if (only && !only.includes(m.id)) continue;
  const full = path.join(root, up);
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
fs.appendFileSync(path.join(root, "evidence/V1-RMD-687/mutations.log"), log.join("\n") + "\n");
