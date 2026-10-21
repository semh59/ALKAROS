import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const dotnet = path.join(os.homedir(), ".dotnet", "dotnet.exe");
const env = { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" };
const project = "tests/Modules/Security/IdentityHardening/ALKAROS.Security.IdentityHardening.Tests.csproj";
const rot = "src/Modules/Security/IdentityHardening/SessionRotationService.cs";
const aud = "src/Modules/Security/IdentityHardening/SuspiciousLoginAuditingAuthenticationService.cs";

const mutants = [
  { id: "R1", file: rot, from: "var effectiveLifetime = lifetime is { } requested && requested < remaining ? requested : remaining;", to: "var effectiveLifetime = lifetime ?? remaining;", filter: "SessionRotationServiceTests" },
  { id: "R2", file: rot, from: "- TimeSpan.FromSeconds(1);", to: ";", filter: "SessionRotationServiceTests" },
  { id: "A1", file: aud, from: "when before is not null && !IsLocked(before, now):", to: "when before is not null:", filter: "SuspiciousLoginAuditingAuthenticationServiceTests" },
  { id: "A2", file: aud, from: "if (after is not null && IsLocked(after, now))", to: "if (after is not null)", filter: "SuspiciousLoginAuditingAuthenticationServiceTests" },
  { id: "A3", file: aud, from: "catch (Exception ex) when (ex is not OperationCanceledException)", to: "catch (Exception ex) when (ex is OperationCanceledException)", filter: "SuspiciousLoginAuditingAuthenticationServiceTests" },
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
    verdict = /Başarısız!/.test(out) ? "KILLED" : /Başarılı!/.test(out) ? "SURVIVED" : "BUILD-ERROR/INFRA";
  } finally {
    fs.writeFileSync(full, orig);
  }
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.writeFileSync(path.join(root, "evidence/V1-RMD-675/mutations.log"), log.join("\n") + "\n");
