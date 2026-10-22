import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const disp = "src/Host/DualScreen/DualScreenStore.Display.cs";
const store = "src/Host/DualScreen/DualScreenStore.cs";
const ep = "src/Host/DualScreen/DualScreenApplication.Endpoints.cs";
const cd = "src/Clients/PosTerminal/src/routes/CustomerDisplay.tsx";
const cashier = "src/Clients/PosTerminal/src/routes/Cashier.tsx";
const api = "src/Clients/PosTerminal/src/api.ts";
const server = "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj";
const serverFilter = "FullyQualifiedName~DualScreenAuthorizationHttpTests|FullyQualifiedName~DualScreenScreensaverTests";
const pos = "src/Clients/PosTerminal";
const mutants = [
  { id: "S1", kind: "server", file: disp, from: "WHERE (display_id = @display_id OR terminal_id = @terminal_id) AND revoked_at IS NULL;", to: "WHERE display_id = @display_id AND revoked_at IS NULL;" },
  { id: "S2", kind: "server", file: store, from: "expires_at = now() + @lifetime", to: "expires_at = expires_at" },
  { id: "S3", kind: "server", file: store, from: "AND (last_seen_at IS NULL OR last_seen_at <= now() - @refresh)", to: "AND (last_seen_at IS NULL OR last_seen_at <= now() + @refresh)" },
  { id: "S10", kind: "server", file: store, from: "(last_seen_at IS NULL OR last_seen_at <= now() - @refresh)", to: "(last_seen_at <= now() - @refresh)" },
  { id: "S4", kind: "server", file: disp, from: "if (row.FailedAttempts >= 5)", to: "if (row.FailedAttempts >= 500)" },
  { id: "S5", kind: "server", file: disp, from: "if (row.ConsumedAt is not null)\n            throw", to: "if (row.ConsumedAt is null)\n            throw" },
  { id: "S6", kind: "server", file: disp, from: "if (row.ExpiresAt <= DateTimeOffset.UtcNow)\n            throw", to: "if (row.ExpiresAt >= DateTimeOffset.UtcNow)\n            throw" },
  { id: "S7", kind: "server", file: ep, from: "$\"/api/v1/customer-displays/pairing-requests/{created.RequestId:D}\"", to: "$\"/api/v1/customer-displays/pairing-requests/{request.DisplayId:D}\"" },
  { id: "S8", kind: "server", file: ep, from: "AppendCookie(context, DisplayCookieName, context.Request.Cookies[DisplayCookieName]!, principal.ExpiresAt);", to: "" },
  { id: "S9", kind: "server", file: disp, from: "WHERE terminal_id = @terminal_id AND revoked_at IS NULL AND expires_at > now()\n            ORDER BY created_at DESC\n            LIMIT 1;\n            \"\"\");\n        command.Parameters.AddWithValue(\"terminal_id\", terminalId);\n        return await", to: "WHERE terminal_id = @terminal_id AND revoked_at IS NOT NULL\n            ORDER BY created_at DESC\n            LIMIT 1;\n            \"\"\");\n        command.Parameters.AddWithValue(\"terminal_id\", terminalId);\n        return await" },
  { id: "H1", kind: "server", file: "src/Host/DualScreen/CustomerDisplayHub.cs", from: "            Context.Abort();\n            return;", to: "            return;" },
  { id: "H2", kind: "server", file: "src/Host/DualScreen/CustomerDisplayHub.cs", from: "DualScreenApplication.TerminalGroup(principal.TerminalId)", to: "DualScreenApplication.TerminalGroup(Guid.Empty)" },
  { id: "C1", kind: "client", file: cd, from: "if (sequence < appliedSequence.current) return;\n      appliedSequence.current = sequence;", to: "appliedSequence.current = sequence;" },
  { id: "C2", kind: "client", file: cd, from: "const timer = window.setTimeout(() => void refresh(), retryDelay(Math.max(0, failedAttempts - 1)));", to: "const timer = window.setTimeout(() => undefined, retryDelay(Math.max(0, failedAttempts - 1)));" },
  { id: "C3", kind: "client", file: cd, from: "reason.status === 429 || ", to: "" },
  { id: "C4", kind: "client", file: cd, from: "if (endedPairingCodes.has(reason.code) || reason.status === 404 || reason.status === 401) {", to: "if (false) {" },
  { id: "C5", kind: "client", file: cd, from: "connection.onclose(() => {\n      if (disposed) return;\n      setFreshness((previous) => ({ ...previous, connectionLost: true }));\n      restartTimer = window.setTimeout(start, retryDelay(attempt++));\n    });", to: "connection.onclose(() => undefined);" },
  { id: "C6", kind: "client", file: cd, from: "connection.onreconnected(() => void refresh());", to: "connection.onreconnected(() => undefined);" },
  { id: "C7", kind: "client", file: cd, from: "açılan pencereye girin", to: "açılan alana girin" },
  { id: "C8", kind: "client", file: cashier, from: "onClick={() => setConfirmRevoke(true)}", to: "onClick={() => void revokeDisplay()}" },
  { id: "C9", kind: "client", file: cashier, from: "setPairingCode(\"\");\n    setConfirmRevoke(false);\n    setError(\"\");", to: "setConfirmRevoke(false);\n    setError(\"\");" },
  { id: "C10", kind: "client", file: cashier, from: "{error && <div className=\"alert error\" role=\"alert\">{error}</div>}\n", to: "" },
  { id: "C11", kind: "client", file: api, from: "if (next === null) break;", to: "break;" },
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
  fs.writeFileSync(full, (crlf ? normalized.replace(m.from, () => m.to).replace(/\n/g, "\r\n") : normalized.replace(m.from, () => m.to)));
  let out;
  try {
    const cmd = m.kind === "server"
      ? `"${process.env.USERPROFILE}/.dotnet/dotnet.exe" test ${server} --filter "${serverFilter}"`
      : `npx vitest run`;
    out = execSync(cmd, {
      cwd: m.kind === "server" ? root : path.join(root, pos), encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 550000,
      env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" },
    });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const verdict = /error CS|error CA/.test(out) ? "BUILD-ERROR" : /Başarısız!|FAILED/.test(out) ? "KILLED" : "SURVIVED";
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-690/mutations.log"), log.join("\n") + "\n");
