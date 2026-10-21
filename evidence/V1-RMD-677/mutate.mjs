import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const dotnet = path.join(os.homedir(), ".dotnet", "dotnet.exe");
const env = { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" };
const rolesTests = "tests/Host/Experience/Roles/ALKAROS.Host.Experience.Roles.Tests.csproj";
const ep = "src/Host/Experience/Roles/RoleManagementEndpoints.cs";
const panel = "src/Clients/PosTerminal/src/features/management-staff-roles/PoliciesPanel.tsx";
const only = process.argv.slice(2);

const mutants = [
  { id: "P1", file: ep, from: "            await authorization.AuthorizeAsync(actor, PermissionCodes.RolesManage, cancellationToken);\n            request.Validate(permissionCode, roleCode);", to: "            request.Validate(permissionCode, roleCode);" },
  { id: "P2", file: ep, from: "MaximumLimit = 1000m;", to: "MaximumLimit = 100000m;" },
  { id: "P3", file: ep, from: "if (!versionMatches)", to: "if (!versionMatches && request.MaxCount < -1)" },
  { id: "P4", file: ep, from: '"identity.policy-changed"', to: '"identity.policy-x"' },
  { id: "P5", file: ep, from: '"identity.policy-removed"', to: '"identity.policy-x"' },
  { id: "P6", file: ep, from: "ApplicationPermissions.RoleWaiter, ApplicationPermissions.RoleCashier,", to: "ApplicationPermissions.RoleWaiter, ApplicationPermissions.RoleCashier, ApplicationPermissions.RoleManager," },
  { id: "P7", file: ep, from: "listing.Where(IsEditablePolicy).Select(ToPolicyV1)", to: "listing.Select(ToPolicyV1)" },
  { id: "C1", file: panel, from: "limitAmount <= 1000", to: "limitAmount <= 100000", client: true },
  { id: "C2", file: panel, from: "current?.rowVersion ?? null", to: "null", client: true },
  { id: "C3", file: panel, from: "{current && <Button", to: "{<Button", client: true },
  { id: "C4", file: panel, from: "maxCount > 100", to: "maxCount > 1000", client: true },
  { id: "C5", file: panel, from: "onClick={() => void act(confirmation.run, confirmation.done)}", to: "onClick={() => void act(async () => undefined, confirmation.done)}", client: true },
];

function run(cmd, cwd = root) {
  try { return execSync(cmd, { cwd, env, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], maxBuffer: 1 << 28, timeout: 540000 }); }
  catch (e) { return (e.stdout ?? "") + (e.stderr ?? "") + "\nEXEC-ERROR"; }
}

const log = [];
for (const m of mutants) {
  if (only.length && !only.includes(m.id)) continue;
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
      const out = run("npx vitest run src/features/management-staff-roles/PoliciesPanel.test.tsx", path.join(root, "src/Clients/PosTerminal"));
      verdict = /failed|FAILED/.test(out) ? "KILLED" : /passed/.test(out) ? "SURVIVED" : "INFRA";
    } else {
      const build = run(`"${dotnet}" build ${rolesTests} -v q`);
      if (/EXEC-ERROR/.test(build)) verdict = "BUILD-ERROR";
      else {
        const out = run(`"${dotnet}" test ${rolesTests} --no-build --filter "FullyQualifiedName~Polic|FullyQualifiedName~AManagerSets|FullyQualifiedName~TheListHides|FullyQualifiedName~AStaleOrMissing|FullyQualifiedName~ALimitOutside|FullyQualifiedName~AnActorWithoutRolesManageCannotSet"`);
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
fs.writeFileSync(path.join(root, "evidence/V1-RMD-677/mutations.log"), log.join("\n") + "\n", { flag: "a" });
