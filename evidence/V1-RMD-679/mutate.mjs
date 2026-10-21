import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const dotnet = path.join(os.homedir(), ".dotnet", "dotnet.exe");
const env = { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" };
const proj = "tests/Host/Experience/Authorization/ALKAROS.Host.Experience.Authorization.Tests.csproj";
const ep = "src/Host/Experience/Authorization/AuthorizationDecisionEndpoints.cs";
const ws = "src/Clients/PosTerminal/src/features/authorization-decisions/AuthorizationDecisionsWorkspace.tsx";
const mutants = [
  { id: "N1", file: ep, from: "RequesterName = names.GetValueOrDefault(grant.RequesterUserId)", to: "RequesterName = null" },
  { id: "N2", file: ep, from: "DelegatorName = names.GetValueOrDefault(item.DelegatorUserId),", to: "DelegatorName = names.GetValueOrDefault(item.GranteeUserId)," },
  { id: "N3", file: ep, from: "UserName = names.GetValueOrDefault(item.UserId)", to: "UserName = null" },
  { id: "N4", file: ep, from: "GranteeName = names.GetValueOrDefault(item.GranteeUserId),", to: "GranteeName = names.GetValueOrDefault(item.DelegatorUserId)," },
  { id: "C1", file: ws, from: "grant.requesterName ?? text.unknownPerson", to: "grant.requesterUserId", client: true },
  { id: "C2", file: ws, from: "delegation.delegatorName ?? text.unknownPerson", to: "delegation.granteeName ?? text.unknownPerson", client: true },
  { id: "C3", file: ws, from: "tightening.userName ?? text.unknownPerson", to: "tightening.userId", client: true },
];
function run(cmd, cwd = root) {
  try { return execSync(cmd, { cwd, env, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], maxBuffer: 1 << 28, timeout: 540000 }); }
  catch (e) { return (e.stdout ?? "") + (e.stderr ?? "") + "\nEXEC-ERROR"; }
}
const log = [];
for (const m of mutants) {
  const full = path.join(root, m.file);
  const orig = fs.readFileSync(full, "utf8");
  const crlf = orig.includes("\r\n");
  const normalized = crlf ? orig.replace(/\r\n/g, "\n") : orig;
  if (!normalized.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); console.log(log.at(-1)); continue; }
  const mutated = normalized.replace(m.from, () => m.to);
  fs.writeFileSync(full, crlf ? mutated.replace(/\n/g, "\r\n") : mutated);
  let verdict;
  try {
    if (m.client) {
      const out = run("npx vitest run src/features/authorization-decisions/AuthorizationDecisionsWorkspace.test.tsx", path.join(root, "src/Clients/PosTerminal"));
      verdict = /failed|FAILED/.test(out) ? "KILLED" : /passed/.test(out) ? "SURVIVED" : "INFRA";
    } else {
      const build = run(`"${dotnet}" build ${proj} -v q`);
      if (/EXEC-ERROR/.test(build)) verdict = "BUILD-ERROR";
      else {
        const out = run(`"${dotnet}" test ${proj} --no-build --filter "FullyQualifiedName~TheThreeListsCarry"`);
        run("taskkill /F /IM testhost.exe");
        verdict = /Başarısız!/.test(out) ? "KILLED" : /Başarılı!/.test(out) ? "SURVIVED" : "INFRA";
      }
    }
  } finally {
    fs.writeFileSync(full, orig);
  }
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.writeFileSync(path.join(root, "evidence/V1-RMD-679/mutations.log"), log.join("\n") + "\n");
