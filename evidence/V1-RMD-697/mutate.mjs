import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const restore = "deploy/docker/restore.sh";
const conf = "deploy/docker/postgresql.tuned.conf";
const compose = "compose.yaml";
const dev = "compose.dev.yaml";
const ignore = ".dockerignore";
const caddy = "deploy/docker/Caddyfile";
const hostComposition = "src/Host/Composition/HostComposition.cs";
const app = "src/Host/DualScreen/DualScreenApplication.cs";
const host = "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj";
const suites = {
  deploy: { project: host, filter: "FullyQualifiedName~DeployFilesTests" },
  lock: { project: host, filter: "FullyQualifiedName~MigrationExecutionTests" },
  stop: { project: host, filter: "FullyQualifiedName~TheHostWaitsForInFlight" },
};
const mutants = [
  { id: "D1", suite: "deploy", file: restore, from: "if [ \"$TARGET_DB\" = \"$LIVE_DB\" ] || [ \"$TARGET_DB\" = \"postgres\" ]; then", to: "if [ \"$TARGET_DB\" = \"postgres\" ]; then" },
  { id: "D2", suite: "deploy", file: restore, from: "if [ \"$TARGET_DB\" = \"$LIVE_DB\" ] || [ \"$TARGET_DB\" = \"postgres\" ]; then", to: "if [ \"$TARGET_DB\" = \"$LIVE_DB\" ]; then" },
  { id: "D3", suite: "deploy", file: restore, from: "LIVE_DB=\"${ALKAROS_LIVE_DB:-alkaros}\"", to: "LIVE_DB=\"${ALKAROS_LIVE_DB:-other}\"" },
  { id: "D4", suite: "deploy", file: restore, from: "  exit 5\nfi", to: "  exit 6\nfi" },
  { id: "A1", suite: "deploy", file: conf, from: " && cp %p /wal-archive/%f.tmp && mv /wal-archive/%f.tmp /wal-archive/%f'", to: " && cp %p /wal-archive/%f'" },
  { id: "A2", suite: "deploy", file: conf, from: "test ! -f /wal-archive/%f && cp", to: "cp" },
  { id: "C1", suite: "deploy", file: compose, from: "      ALKAROS_BACKUP_DIR: /backups\n", to: "" },
  { id: "C2", suite: "deploy", file: compose, from: "      ALKAROS_OFFSITE_BACKUP_DIR: /offsite-backup\n", to: "" },
  { id: "C3", suite: "deploy", file: compose, from: "      - alkaros-backups:/backups:ro\n", to: "" },
  { id: "C4", suite: "deploy", file: compose, from: "    stop_grace_period: 50s\n", to: "" },
  { id: "I1", suite: "deploy", file: ignore, from: "\n.git\n", to: "\n" },
  { id: "I2", suite: "deploy", file: ignore, from: "\n.env\n", to: "\n" },
  { id: "P1", suite: "deploy", file: dev, from: "      - \"127.0.0.1:5080:5080\"", to: "      - \"5080:5080\"" },
  { id: "P2", suite: "deploy", file: dev, from: "      - \"127.0.0.1:8092:82\"", to: "      - \"8092:82\"" },
  { id: "K1", suite: "deploy", file: caddy, from: "		@revalidate not path /assets/*\n", to: "		@revalidate not path /nothing/*\n" },
  { id: "L1", suite: "lock", file: hostComposition, from: "SELECT pg_try_advisory_lock(@key);", to: "SELECT true;" },
  { id: "L2", suite: "lock", file: hostComposition, from: "        if (!TryLockMigrations(dataSource, output, out var lockConnection))\n            return HostExitCode.StartupFailed;\n        using var heldLock = lockConnection;\n\n        var forwardFiles = discovery.Files\n            .Where(f => f.Kind == MigrationScriptKind.Up)\n            .ToDictionary(f => f.Id, StringComparer.Ordinal);\n        var step", to: "        var forwardFiles = discovery.Files\n            .Where(f => f.Kind == MigrationScriptKind.Up)\n            .ToDictionary(f => f.Id, StringComparer.Ordinal);\n        var step" },
  { id: "L3", suite: "lock", file: hostComposition, from: "        if (!TryLockMigrations(dataSource, output, out var lockConnection))\n            return HostExitCode.StartupFailed;\n        using var heldLock = lockConnection;\n\n        var forwardFiles = discovery.Files\n            .Where(f => f.Kind == MigrationScriptKind.Up)\n            .ToDictionary(f => f.Id, StringComparer.Ordinal);\n\n        var result", to: "        var forwardFiles = discovery.Files\n            .Where(f => f.Kind == MigrationScriptKind.Up)\n            .ToDictionary(f => f.Id, StringComparer.Ordinal);\n\n        var result" },
  { id: "L4", suite: "lock", file: hostComposition, from: "            connection.Dispose();\n            output.WriteLine(\"Another migration run holds the migration lock; nothing was executed.\");\n            return false;", to: "            connection.Dispose();\n            return true;" },
  { id: "S1", suite: "stop", file: app, from: "        builder.Services.Configure<HostOptions>(host => host.ShutdownTimeout = ShutdownTimeout);\n", to: "" },
  { id: "S2", suite: "stop", file: app, from: "public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(35);", to: "public static readonly TimeSpan ShutdownTimeout = TimeSpan.FromSeconds(60);" },
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
    const suite = suites[m.suite];
    out = execSync(`"${process.env.USERPROFILE}/.dotnet/dotnet.exe" test ${suite.project} --filter "${suite.filter}"`, {
      cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 550000,
      env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" },
    });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const verdict = /error CS|error CA/.test(out) ? "BUILD-ERROR" : /Başarısız!|FAILED/.test(out) ? "KILLED" : "SURVIVED";
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-697/mutations.log"), log.join("\n") + "\n");
