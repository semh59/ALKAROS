import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const client = "src/Clients/PosTerminal";
const sec = `${client}/src/features/management-security-system`;
const store = "src/Host/DualScreen/DualScreenStore.TerminalKey.cs";
const app = "src/Host/DualScreen/DualScreenApplication.Endpoints.cs";
const admin = "src/Host/Experience/SecurityAdministration/SecurityAdministrationEndpoints.cs";
const migration = "database/migrations/V1/V1-RMD-701/202-terminal-key-hash.up.sql";
const hostTests = "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj";
const adminTests = "tests/Host/Experience/SecurityAdministration/ALKAROS.Host.Experience.SecurityAdministration.Tests.csproj";

const suites = {
  store: { kind: "dotnet", project: hostTests, filter: "FullyQualifiedName~TerminalKeyTests" },
  login: { kind: "dotnet", project: hostTests, filter: "FullyQualifiedName~ATerminalIdBelongsToTheBrowser|FullyQualifiedName~AWrongPasswordNeverClaims" },
  admin: { kind: "dotnet", project: adminTests, filter: "FullyQualifiedName~AManagerListsTheTerminals" },
  panel: { kind: "vitest", files: "src/features/management-security-system" },
};

const mutants = [
  { id: "S1", suite: "store", file: store, from: "&& DualScreenToken.Matches(presentedKey ?? string.Empty, storedHash)", to: "&& (true || DualScreenToken.Matches(presentedKey ?? string.Empty, storedHash))" },
  { id: "S2", suite: "store", file: store, from: "claim.Parameters.AddWithValue(\"hash\", hash);", to: "claim.Parameters.AddWithValue(\"hash\", raw);" },
  { id: "S3", suite: "store", file: store, from: "WHERE terminal_id = @terminal_id AND terminal_key_hash IS NULL;\"))", to: "WHERE terminal_id = @terminal_id;\"))" },
  { id: "S4", suite: "store", file: store, from: "SET terminal_key_hash = NULL, row_version", to: "SET terminal_key_hash = terminal_key_hash, row_version" },
  { id: "S5", suite: "store", file: store, from: "        await audit(connection, transaction, cancellationToken);\n", to: "" },
  { id: "S6", suite: "store", file: store, from: "t.terminal_key_hash IS NOT NULL, t.created_at", to: "FALSE, t.created_at" },
  { id: "S7", suite: "login", file: app, from: "if (keyResult == TerminalKeyResult.Rejected)", to: "if (false && keyResult == TerminalKeyResult.Rejected)" },
  { id: "S8", suite: "login", file: app, from: "if (newTerminalKey is not null)\n                AppendCookie", to: "if (false && newTerminalKey is not null)\n                AppendCookie" },
  { id: "S9", suite: "login", file: app, from: "$\"alkaros.terminal-key.{terminalId:N}\"", to: "\"alkaros.terminal-key\"" },
  { id: "S10", suite: "login", file: app, from: "\"TERMINAL_KEY_MISMATCH\"", to: "\"INVALID_CREDENTIALS\"" },
  { id: "S11", suite: "admin", file: admin, from: "eventName: \"security.terminal-binding-reset\"", to: "eventName: \"security.terminal-reset\"" },
  { id: "S12", suite: "admin", file: admin, from: "return reset\n                ? Results.Ok(new TerminalBindingResetV1(terminalId))", to: "return !reset\n                ? Results.Ok(new TerminalBindingResetV1(terminalId))" },
  { id: "S13", suite: "store", file: migration, from: "CHECK (terminal_key_hash IS NULL OR terminal_key_hash ~ '^[0-9A-F]{64}$')", to: "CHECK (true)" },
  { id: "S14", suite: "panel", file: `${sec}/api.ts`, from: "/security/terminals/${terminalId}/reset-binding", to: "/security/terminals/${terminalId}/reset" },
  { id: "S15", suite: "panel", file: `${sec}/TerminalsPanel.tsx`, from: "disabled={busy || !terminal.bound}", to: "disabled={busy}" },
  { id: "S16", suite: "panel", file: `${sec}/TerminalsPanel.tsx`, from: "      reload();\n", to: "" },
  { id: "S17", suite: "panel", file: `${sec}/SecuritySection.tsx`, from: "    <TerminalsPanel client={client} />\n", to: "" },
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
  const suite = suites[m.suite];
  let out;
  try {
    const command = suite.kind === "vitest"
      ? `npx vitest run ${suite.files}`
      : `"${process.env.USERPROFILE}/.dotnet/dotnet.exe" test ${suite.project} --filter "${suite.filter}"`;
    out = execSync(command, {
      cwd: suite.kind === "vitest" ? path.join(root, client) : root,
      encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 550000,
      env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" },
    });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const verdict = /error CS|error CA|error TS/.test(out) ? "BUILD-ERROR" : /Başarısız!|FAILED/.test(out) ? "KILLED" : "SURVIVED";
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-701/mutations.log"), log.join("\n") + "\n");
