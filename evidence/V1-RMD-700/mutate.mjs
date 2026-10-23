import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const ssl = "src/Integrations/QrRelay/LocalConnector/DatabaseSslSettings.cs";
const restore = "src/Host/Experience/SecurityAdministration/HostDatabaseConnection.cs";
const runner = "src/Host/Composition/Migrations/PsqlScriptRunner.cs";
const hba = "deploy/docker/pg_hba.conf";
const conf = "deploy/docker/postgresql.tuned.conf";
const compose = "compose.yaml";
const ops = "compose.ops.yaml";
const roles = "deploy/docker/roles.sql";
const tlsScript = "deploy/docker/generate-db-tls.sh";
const hostTests = "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj";
const filter = "FullyQualifiedName~DeployFilesTests|FullyQualifiedName~DatabaseSslTests|FullyQualifiedName~DatabaseRolesTests";

const mutants = [
  { id: "M1", file: ssl, from: "return IsLoopback(host) ? SslMode.Prefer : SslMode.Require;", to: "return SslMode.Prefer;" },
  { id: "M2", file: ssl, from: "\"verify-ca\" => SslMode.VerifyCA,", to: "\"verify-ca\" => SslMode.Require," },
  { id: "M3", file: ssl, from: "\"require\" => SslMode.Require,", to: "\"require\" => SslMode.Prefer," },
  { id: "M4", file: ssl, from: "builder.SslMode = ModeFor(builder.Host ?? string.Empty);", to: "" },
  { id: "M5", file: ssl, from: "builder.RootCertificate = root;", to: "{ }" },
  { id: "M6", file: runner, from: "startInfo.Environment[\"PGSSLMODE\"]", to: "startInfo.Environment[\"PGSSLMODE_OFF\"]" },
  { id: "M7", file: runner, from: "startInfo.Environment[\"PGSSLROOTCERT\"] = rootCertificate;", to: "" },
  { id: "M8", file: restore, from: "Username = user.Trim(),", to: "" },
  { id: "M9", file: restore, from: "Password = File.ReadAllText(passwordFile.Trim()).Trim('\\r', '\\n'),", to: "Password = File.ReadAllText(passwordFile.Trim())," },
  { id: "M10", file: hba, from: "hostssl all             all             samenet", to: "host    all             all             samenet" },
  { id: "M11", file: hba, from: "hostnossl all           all             all                     reject\n", to: "" },
  { id: "M12", file: conf, from: "ssl_min_protocol_version = 'TLSv1.2'", to: "ssl_min_protocol_version = 'TLSv1'" },
  { id: "M13", file: compose, from: "cap_drop: [ALL]", to: "cap_drop: []", all: false },
  { id: "M14", file: compose, from: "no-new-privileges:true", to: "no-new-privileges:false" },
  { id: "M15", file: compose, from: "serve --db-url postgresql://alkaros_app@postgres", to: "serve --db-url postgresql://alkaros@postgres" },
  { id: "M16", file: compose, from: "      - ./deploy/docker/roles.sql:/roles.sql:ro\n", to: "" },
  { id: "M17", file: ops, from: "PGSSLMODE: verify-full", to: "PGSSLMODE: require" },
  { id: "M18", file: roles, from: "LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L',\n              :'app_role'", to: "LOGIN NOSUPERUSER CREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD %L',\n              :'app_role'" },
  { id: "M19", file: roles, from: "REVOKE CREATE ON SCHEMA public FROM PUBLIC;", to: "" },
  { id: "M20", file: roles, from: "REVOKE ALL ON %s FROM %I", to: "GRANT ALL ON %s TO %I" },
  { id: "M21", file: tlsScript, from: "DNS:postgres, ", to: "" },
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
  fs.writeFileSync(full, crlf ? normalized.replace(m.from, () => m.to).replace(/\n/g, "\r\n") : normalized.replace(m.from, () => m.to));
  let out;
  try {
    out = execSync(`"${process.env.USERPROFILE}/.dotnet/dotnet.exe" test ${hostTests} --filter "${filter}"`, {
      cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 550000,
      env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" },
    });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const verdict = /error CS|error CA/.test(out) ? "BUILD-ERROR" : /Başarısız!|FAILED/.test(out) ? "KILLED" : "SURVIVED";
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-700/mutations.log"), log.join("\n") + "\n");
