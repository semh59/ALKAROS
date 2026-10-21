import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const dotnet = path.join(os.homedir(), ".dotnet", "dotnet.exe");
const env = { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" };
const project = "tests/Modules/Identity/Authorization/ALKAROS.Identity.Authorization.Tests.csproj";
const up = "database/migrations/V1/V1-RMD-674/196-waiter-discount-policy.up.sql";
const down = "database/migrations/V1/V1-RMD-674/196-waiter-discount-policy.down.sql";
const repo = "src/Modules/Identity/Authorization/Delegations/PostgresAuthorizationDelegationRepository.cs";

const mutants = [
  { id: "S1", file: up, from: "100.00, 10, 28800", to: "1000.00, 10, 28800", filter: "WaiterDiscountPolicySeedTests" },
  { id: "S2", file: up, from: "ON CONFLICT (permission_code, role_code) DO NOTHING", to: "ON CONFLICT (permission_code, role_code) DO UPDATE SET limit_amount = 100.00", filter: "WaiterDiscountPolicySeedTests" },
  { id: "S3", file: up, from: "VALUES ('bills.discount', 'waiter',", to: "VALUES ('bills.comp', 'waiter',", filter: "WaiterDiscountPolicySeedTests" },
  { id: "S4", file: down, from: "AND limit_amount = 100.00 AND max_count = 10 AND window_seconds = 28800 AND updated_by IS NULL AND row_version = 1", to: "", filter: "WaiterDiscountPolicySeedTests" },
  { id: "R1", file: repo, from: "AND NOT delegator.active)", to: "AND delegator.active IS NULL)", filter: "ADelegationStopsCoveringOnceItsDelegatorIsDeactivated" },
];

function run(cmd) {
  try { return execSync(cmd, { cwd: root, env, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], maxBuffer: 1 << 28, timeout: 300000 }); }
  catch (e) { return (e.stdout ?? "") + (e.stderr ?? "") + "\nEXEC-ERROR"; }
}

const log = [];
for (const m of mutants) {
  const full = path.join(root, m.file);
  const orig = fs.readFileSync(full, "utf8");
  const crlf = orig.includes("\r\n");
  const normalized = crlf ? orig.replace(/\r\n/g, "\n") : orig;
  if (!normalized.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); console.log(log.at(-1)); continue; }
  let mutated = normalized.replace(m.from, () => m.to);
  if (crlf) mutated = mutated.replace(/\n/g, "\r\n");
  fs.writeFileSync(full, mutated);
  let verdict;
  try {
    const out = run(`"${dotnet}" test ${project} --filter "FullyQualifiedName~${m.filter}"`);
    run("taskkill /F /IM testhost.exe");
    verdict = /Başarısız!/.test(out) ? "KILLED" : /Başarılı!/.test(out) ? "SURVIVED" : /EXEC-ERROR/.test(out) ? "BUILD-ERROR" : "INFRA";
  } finally {
    fs.writeFileSync(full, orig);
  }
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.writeFileSync(path.join(root, "evidence/V1-RMD-674/mutations.log"), log.join("\n") + "\n");
