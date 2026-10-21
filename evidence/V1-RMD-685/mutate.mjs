import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const ep = "src/Host/Experience/Settings/BusinessIdentityLogoEndpoints.cs";
const st = "src/Modules/Settings/BusinessIdentity/BusinessLogoStore.cs";
const hostProj = "tests/Host/Experience/Settings/ALKAROS.Host.Experience.Settings.Tests.csproj";
const storeProj = "tests/Modules/Settings/BusinessIdentity/ALKAROS.Settings.BusinessIdentity.Tests.csproj";
const mutants = [
  { id: "H1", file: ep, from: ".Trim().ToLowerInvariant()", to: ".Trim()" },
  { id: "H2", file: ep, from: "file.ContentType.Split(';')[0].Trim()", to: "file.ContentType.Trim()" },
  { id: "H3", file: ep, from: "file.Length > MaxLogoImageBytes", to: "file.Length >= MaxLogoImageBytes" },
  { id: "H4", file: ep, from: "content[7] == 0x0A;", to: "true;" },
  { id: "H5", file: ep, from: "content[2] == 0xFF;", to: "true;" },
  { id: "H6", file: ep, from: "content[11] == 'P';", to: "true;" },
  { id: "H7", file: ep, from: "content[8] == 'W'", to: "true" },
  { id: "H8", file: ep, from: "content[3] == 'F'", to: "true" },
  { id: "H9", file: ep, from: '"settings.business-logo-changed"', to: '"settings.x"' },
  { id: "H10", file: ep, from: '"settings.business-logo-removed"', to: '"settings.x"' },
  { id: "H11", file: ep, from: "afterStateJson: afterStateJson)", to: "afterStateJson: null)" },
  { id: "H12", file: ep, from: "actorId: SettingsManagerEndpointFilter.RequireActorId(context)", to: "actorId: Guid.Empty" },
  { id: "H13", file: ep, from: '"image/jpeg" => HasJpegMagicNumber(content)', to: '"image/jpeg" => true' },
  { id: "H14", file: ep, from: "await store.SaveAsync(content, contentType, cancellationToken);", to: "await store.SaveAsync(content, file.ContentType, cancellationToken);" },
  { id: "H15", file: ep, from: "content[0] == 0x89 &&", to: "true &&" },
  { id: "S1", file: st, from: "SELECT content_type, updated_at FROM", to: "SELECT content_type, now() FROM", proj: storeProj },
  { id: "S2", file: st, from: "internal static string ETagFor(DateTimeOffset updatedAt) => $\"\\\"{updatedAt.UtcTicks:x}\\\"\";", to: "internal static string ETagFor(DateTimeOffset updatedAt) => \"\\\"x\\\"\";", proj: storeProj },
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
    out = execSync(`"${process.env.USERPROFILE}/.dotnet/dotnet.exe" test ${m.proj ?? hostProj} ${m.proj ? "" : '--filter "FullyQualifiedName~BusinessIdentityLogoEndpointsTests"'}`, {
      cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 500000,
      env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" },
    });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const verdict = /error CS|error CA/.test(out) ? "BUILD-ERROR" : /Başarısız!|FAILED/.test(out) ? "KILLED" : "SURVIVED";
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-685/mutations.log"), log.join("\n") + "\n");
