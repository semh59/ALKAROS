import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const app = path.join(root, "src/Clients/PosTerminal");
const h = "src/Clients/PosTerminal/src/features/management-settings-history/";
const mutants = [
  { id: "C1", file: h + "models.ts", from: 'value === "0" ? "Sınırsız"', to: 'value === "0" ? value' },
  { id: "C2", file: h + "models.ts", from: 'timeZone: "Europe/Istanbul"', to: 'timeZone: "UTC"' },
  { id: "C3", file: h + "models.ts", from: '  "Setting deactivated": "Ayar pasifleştirildi",\n', to: "" },
  { id: "C4", file: h + "models.ts", from: 'String(hours).padStart(2, "0")', to: 'String(hours).padStart(1, "0")' },
  { id: "C5", file: h + "models.ts", from: "min: 0, max: 50", to: "min: 0, max: 500" },
  { id: "C6", file: h + "SettingEditor.tsx", from: 'if (reason.trim() === "")', to: 'if (reason.trim() === "" && false)' },
  { id: "C7", file: h + "SettingEditor.tsx", from: "if (newValue === setting.value) return", to: "if (newValue === setting.value && false) return" },
  { id: "C8", file: h + "SettingEditor.tsx", from: "failure.status === 409", to: "failure.status === 999" },
  { id: "C9", file: h + "SettingsHistorySection.tsx", from: "entry.changedByName ?? t.unknownPerson", to: "entry.changedBy.slice(0, 8)" },
  { id: "C10", file: "src/Clients/PosTerminal/src/features/management/panel.tsx", from: 'if (mine === latest.current) setPanel({ state: "ready", data });', to: 'setPanel({ state: "ready", data });' },
  { id: "C11", file: "src/Clients/PosTerminal/src/routes/BusinessIdentitySettings.tsx", from: "versions.current[BusinessNameKey] ?? (await api.getSetting(BusinessNameKey)).rowVersion", to: "(await api.getSetting(BusinessNameKey)).rowVersion" },
  { id: "C12", file: "src/Clients/PosTerminal/src/routes/BusinessIdentitySettings.tsx", from: "reason.status === 401) {\n      setSession", to: "reason.status === 999) {\n      setSession" },
  { id: "C13", file: "src/Clients/PosTerminal/src/routes/BusinessIdentitySettings.tsx", from: "onClick={() => setConfirmingRemove(true)}", to: "onClick={() => void removeLogo()}" },
  { id: "C14", file: "src/Clients/PosTerminal/src/routes/BusinessIdentitySettings.tsx", from: "{!loaded && <button", to: "{false && <button" },
  { id: "C15", file: h + "api.ts", from: 'method: "PUT"', to: 'method: "POST"' },
  { id: "C16", file: "src/Clients/PosTerminal/src/routes/BusinessIdentitySettings.tsx", from: "reason.status === 409) {\n      await loadCurrentState", to: "reason.status === 999) {\n      await loadCurrentState" },
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
  fs.writeFileSync(full, (crlf ? normalized.replace(m.from, () => m.to).replace(/\n/g, "\r\n") : normalized.replace(m.from, () => m.to)));
  let out;
  try {
    out = execSync("npx vitest run src/features/management-settings-history src/routes/BusinessIdentitySettings.test.tsx", { cwd: app, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 240000 });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  log.push(`${m.id} ${/FAILED|failed/.test(out) ? "KILLED" : "SURVIVED"} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-684/mutations.log"), log.join("\n") + "\n");
