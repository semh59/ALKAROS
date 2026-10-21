import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const dotnet = path.join(os.homedir(), ".dotnet", "dotnet.exe");
const env = { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" };
const project = "tests/Modules/Identity/Authorization/ALKAROS.Identity.Authorization.Tests.csproj";
const sql = "database/migrations/V1/V1-RMD-673/195-authorization-guards.up.sql";

const mutants = [
  { id: "M1", from: "CHECK (approver_user_id IS NULL OR approver_user_id <> requester_user_id) NOT VALID;", to: "CHECK (approver_user_id IS NULL OR approver_user_id IS NULL) NOT VALID;" },
  { id: "M2", from: "       OR NEW.requester_role_code IS DISTINCT FROM OLD.requester_role_code\n", to: "" },
  { id: "M3", from: "       OR NEW.subject_serving_user_id IS DISTINCT FROM OLD.subject_serving_user_id\n", to: "" },
  { id: "M4", from: "       OR NEW.limit_amount IS DISTINCT FROM OLD.limit_amount\n", to: "" },
  { id: "M5", from: "       OR NEW.expires_at IS DISTINCT FROM OLD.expires_at THEN", to: "       OR false THEN" },
  { id: "M6", from: "        RAISE EXCEPTION 'identity.authorization_delegations rows cannot be deleted.';", to: "        RETURN OLD;" },
  { id: "M7", from: "    IF OLD.revoked_at IS NOT NULL THEN", to: "    IF false THEN" },
];

function run(cmd) {
  try { return execSync(cmd, { cwd: root, env, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], maxBuffer: 1 << 28, timeout: 300000 }); }
  catch (e) { return (e.stdout ?? "") + (e.stderr ?? "") + "\nEXEC-ERROR"; }
}

const full = path.join(root, sql);
const log = [];
for (const m of mutants) {
  const orig = fs.readFileSync(full, "utf8");
  const crlf = orig.includes("\r\n");
  const normalized = crlf ? orig.replace(/\r\n/g, "\n") : orig;
  if (!normalized.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); console.log(log.at(-1)); continue; }
  let mutated = normalized.replace(m.from, () => m.to);
  if (crlf) mutated = mutated.replace(/\n/g, "\r\n");
  fs.writeFileSync(full, mutated);
  let verdict;
  try {
    const out = run(`"${dotnet}" test ${project} --filter "FullyQualifiedName~AuthorizationGuardDatabaseTests"`);
    run("taskkill /F /IM testhost.exe");
    verdict = /Başarısız!/.test(out) ? "KILLED" : /Başarılı!/.test(out) ? "SURVIVED" : "INFRA";
  } finally {
    fs.writeFileSync(full, orig);
  }
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.writeFileSync(path.join(root, "evidence/V1-RMD-673/mutations.log"), log.join("\n") + "\n");
