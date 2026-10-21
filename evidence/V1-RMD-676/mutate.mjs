import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const dotnet = path.join(os.homedir(), ".dotnet", "dotnet.exe");
const env = { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" };
const project = "tests/Host/Experience/OfflineReconciliation/ALKAROS.Host.Experience.OfflineReconciliation.Tests.csproj";
const file = "src/Host/Experience/OfflineReconciliation/OfflineReconciliationEndpoints.cs";
const check = `            if (request.Actions is null || request.Actions.Count > MaxActionsPerBatch)
                throw new ArgumentException("A reconnect batch carries at most " + MaxActionsPerBatch + " actions.");
`;
const full = path.join(root, file);
const orig = fs.readFileSync(full, "utf8");
const crlf = orig.includes("\r\n");
let text = crlf ? orig.replace(/\r\n/g, "\n") : orig;
const log = [];
if (!text.includes(check)) { console.log("NOT-APPLIED"); process.exit(1); }
text = text.replace(check, "").replace("            var cashierToken", check + "            var cashierToken");
fs.writeFileSync(full, crlf ? text.replace(/\n/g, "\r\n") : text);
let verdict;
try {
  const run = (cmd) => { try { return execSync(cmd, { cwd: root, env, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], maxBuffer: 1 << 28, timeout: 400000 }); } catch (e) { return (e.stdout ?? "") + (e.stderr ?? "") + "\nEXEC-ERROR"; } };
  const out = run(`"${dotnet}" test ${project} --filter "FullyQualifiedName~AnOversizedBatchWithoutASession"`);
  run("taskkill /F /IM testhost.exe");
  verdict = /Başarısız!/.test(out) ? "KILLED" : /Başarılı!/.test(out) ? "SURVIVED" : "INFRA";
} finally {
  fs.writeFileSync(full, orig);
}
log.push(`M1 ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
console.log(log[0]);
fs.writeFileSync(path.join(root, "evidence/V1-RMD-676/mutations.log"), log.join("\n") + "\n");
