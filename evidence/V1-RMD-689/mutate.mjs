import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const exp = "src/Host/Experience/HelpRequests/HelpRequestExperience.cs";
const store = "src/Host/Experience/HelpRequests/HelpRequestStore.cs";
const hub = "src/Host/Experience/HelpRequests/HelpRequestHub.cs";
const proj = "tests/Host/Experience/HelpRequests/ALKAROS.Host.Experience.HelpRequests.Tests.csproj";
const mutants = [
  { id: "M1", file: exp, from: "if (request.TableId == Guid.Empty)", to: "if (request.TableId == Guid.NewGuid())" },
  { id: "M2", file: exp, from: "context.Response.Headers.RetryAfter = retryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);", to: "" },
  { id: "M3", file: exp, from: "var connectedRecipients = recipients.Count;", to: "var connectedRecipients = 1;" },
  { id: "M4", file: exp, from: "if (await ResolveManagerAsync(dataSource, context, cancellationToken) is null)\n                return UnauthorizedResult();\n\n            return Results.Ok(await store.ListOpenAsync", to: "if (await ResolveManagerAsync(dataSource, context, cancellationToken) is not null)\n                return UnauthorizedResult();\n\n            return Results.Ok(await store.ListOpenAsync" },
  { id: "M5", file: exp, from: "if (await ResolveManagerAsync(dataSource, context, cancellationToken) is not Guid managerId)\n                return UnauthorizedResult();", to: "var managerId = Guid.NewGuid();" },
  { id: "M6", file: exp, from: "return Results.NotFound(new { error = new { code = \"NOT_FOUND\", message = \"Yardım çağrısı bulunamadı.\" } });", to: "return Results.NoContent();" },
  { id: "M7", file: exp, from: "WaiterOrderStatusHub.GroupName(requesterId)", to: "WaiterOrderStatusHub.GroupName(Guid.Empty)" },
  { id: "M8", file: store, from: "WHERE table_id = @table_id AND acknowledged_at IS NULL\n            ORDER BY created_at DESC", to: "WHERE table_id = @table_id\n            ORDER BY created_at DESC" },
  { id: "M9", file: store, from: "WHERE h.acknowledged_at IS NULL AND h.created_at > now() - interval '24 hours'", to: "WHERE h.created_at > now() - interval '24 hours'" },
  { id: "M10", file: store, from: "WHERE h.table_id = target.table_id AND h.acknowledged_at IS NULL", to: "WHERE h.help_request_id = @help_request_id AND h.acknowledged_at IS NULL" },
  { id: "M11", file: hub, from: "_recipients.Connected(Context.ConnectionId);", to: "" },
  { id: "M12", file: hub, from: "_recipients.Disconnected(Context.ConnectionId);", to: "" },
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
    out = execSync(`"${process.env.USERPROFILE}/.dotnet/dotnet.exe" test ${proj}`, {
      cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 500000,
      env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" },
    });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const verdict = /error CS|error CA/.test(out) ? "BUILD-ERROR" : /Başarısız!|FAILED/.test(out) ? "KILLED" : "SURVIVED";
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-689/mutations.log"), log.join("\n") + "\n");
