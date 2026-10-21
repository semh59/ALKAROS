import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const dotnet = path.join(os.homedir(), ".dotnet", "dotnet.exe");
const env = { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" };
const only = process.argv.slice(2);

const rolesProject = 'tests/Host/Experience/Roles/ALKAROS.Host.Experience.Roles.Tests.csproj';
const authzProject = 'tests/Modules/Identity/Authorization/ALKAROS.Identity.Authorization.Tests.csproj';
const ep = 'src/Host/Experience/Roles/RoleManagementEndpoints.cs';
const svc = 'src/Modules/Identity/Authorization/RoleManagementService.cs';
const repo = 'src/Modules/Identity/Authorization/PostgresRoleRepository.cs';
const f = 'FullyQualifiedName~EveryManagementCommandLeavesOneAuditRow';
const mutants = [
  { id: 'A1', file: ep, from: '"identity.role-created"', to: '"identity.role-createdX"', project: rolesProject, filter: f },
  { id: 'A2', file: ep, from: '"identity.permission-added"', to: '"identity.permission-addedX"', project: rolesProject, filter: f },
  { id: 'A3', file: ep, from: '"identity.role-permission-assigned"', to: '"identity.role-permission-assignedX"', project: rolesProject, filter: f },
  { id: 'A4', file: ep, from: '"identity.role-permission-revoked"', to: '"identity.role-permission-revokedX"', project: rolesProject, filter: f },
  { id: 'A5', file: ep, from: '"identity.user-created"', to: '"identity.user-createdX"', project: rolesProject, filter: f },
  { id: 'A6', file: ep, from: '"identity.user-role-assigned"', to: '"identity.user-role-assignedX"', project: rolesProject, filter: f },
  { id: 'A7', file: ep, from: '"identity.user-role-revoked"', to: '"identity.user-role-revokedX"', project: rolesProject, filter: f },
  { id: 'A8', file: ep, from: 'actorId: ActorId(http),', to: 'actorId: null,', project: rolesProject, filter: f },
  { id: 'S1', file: svc, from: 'await EnsureAnotherRoleManagerRemainsAsync(roleId, null, cancellationToken);', to: '', project: authzProject, filter: 'FullyQualifiedName~RoleManagementGuardTests' },
  { id: 'S2', file: svc, from: 'await EnsureAnotherRoleManagerRemainsAsync(roleId, userId, cancellationToken);', to: '', project: authzProject, filter: 'FullyQualifiedName~RoleManagementGuardTests' },
  { id: 'S3', file: svc, from: 'if (remaining == 0 && ', to: 'if (remaining < 0 && ', project: authzProject, filter: 'FullyQualifiedName~RoleManagementGuardTests' },
  { id: 'S4', file: repo, from: 'WHERE u.active AND p.code = @code', to: 'WHERE p.code = @code', project: authzProject, filter: 'FullyQualifiedName~RoleManagementGuardTests' },
  { id: 'S5', file: svc, from: 'username = username.Trim();', to: '', project: authzProject, filter: 'FullyQualifiedName~RoleManagementGuardTests' },
  { id: 'S6', file: repo, from: '(@user_id::uuid IS NULL OR ur.user_id = @user_id)', to: 'true', project: authzProject, filter: 'FullyQualifiedName~RoleManagementGuardTests' },
  { id: 'C1', file: 'src/Host/Composition/Errors/ApiErrorCatalog.cs', from: 'LastRoleManagerException =>', to: 'LastRoleManagerException when false =>', project: rolesProject, filter: 'FullyQualifiedName~TheLastRoleManagerCannotBeStripped' },
];

function run(cmd) {
  try {
    return execSync(cmd, { cwd: root, env, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], maxBuffer: 1 << 28, timeout: 540000 });
  } catch (e) {
    return (e.stdout ?? "") + (e.stderr ?? "") + "\nEXEC-ERROR";
  }
}

const log = [];
for (const m of mutants) {
  if (only.length && !only.includes(m.id)) continue;
  const full = path.join(root, m.file);
  const orig = path.join(os.tmpdir(), `m668-${m.id}.orig`);
  fs.copyFileSync(full, orig);
  let text = fs.readFileSync(full, "utf8");
  if (!text.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); continue; }
  fs.writeFileSync(full, text.replace(m.from, m.to));
  let verdict;
  try {
    const build = run(`"${dotnet}" build ${m.project} -v q`);
    if (/EXEC-ERROR/.test(build) && !/Hata\s+0|0 Hata/.test(build)) { verdict = "BUILD-ERROR"; }
    else {
      const out = run(`"${dotnet}" test ${m.project} --no-build --filter "${m.filter}"`);
      run("taskkill /F /IM testhost.exe");
      if (/Başarısız!/.test(out)) verdict = "KILLED";
      else if (/Başarılı!/.test(out)) verdict = "SURVIVED";
      else verdict = "INFRA";
    }
  } finally {
    fs.copyFileSync(orig, full);
  }
  const same = fs.readFileSync(full, "utf8") === fs.readFileSync(orig, "utf8");
  log.push(`${m.id} ${verdict} restored=${same} (${m.file})`);
  console.log(log[log.length - 1]);
}
fs.writeFileSync(path.join(root, "evidence/V1-RMD-668/mutations.log"), log.join("\n") + "\n", { flag: only.length ? "a" : "w" });
