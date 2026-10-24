import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const log = `${root}/evidence/V1-RMD-717/mutations.log`;
const host = "~/.dotnet/dotnet.exe test tests/Host/Experience/Tables -nologo --filter FullyQualifiedName~TableIdempotency";
const ui = 'cd src/Clients/PosTerminal && export PATH="$PATH:/c/Program Files/nodejs" && npx vitest run src/features/tables';
const F = "src/Host/Experience/Tables/TableIdempotencyFilter.cs";

const mutants = [
  { id: "I1", file: F, cmd: host, from: "if (found.RequestHash != requestHash)", to: "if (false)", note: "the same key with another request replays the first answer" },
  { id: "I2", file: F, cmd: host, from: '"SELECT pg_advisory_lock(hashtextextended(@scope, 0));"', to: '"SELECT 1 WHERE @scope IS NOT NULL;"', note: "two requests with one key run at the same time" },
  { id: "I3", file: F, cmd: host, from: '$"tables:{terminalId:N}:{principal.UserId:N}"', to: '$"tables:{terminalId:N}:"', note: "the key is not scoped to the signed-in user" },
  { id: "I4", file: F, cmd: host, from: "StatusCodes.Status200OK })\n                await StoreAsync", to: "StatusCodes.Status201Created })\n                await StoreAsync", note: "a successful answer is never stored" },
  { id: "I5", file: F, cmd: host, from: "AND expires_at > now();", to: ";", note: "an expired key still replays" },
  { id: "C1", file: "src/Clients/PosTerminal/src/features/tables/tableApi.ts", cmd: ui, from: '...(options.idempotencyKey ? { "X-Idempotency-Key": options.idempotencyKey } : {}),', to: "", note: "the client sends no key" },
  { id: "C2", file: "src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx", cmd: ui, from: "    actionKey.current = crypto.randomUUID();\n", to: "", note: "the dialog opens without a key" },
  { id: "C3", file: "src/Clients/PosTerminal/src/features/tables/TableWorkspace.tsx", cmd: ui, from: "        idempotencyKey: actionKey.current,\n", to: "", note: "the dialog drops the key" },
];

const only = process.argv.slice(2);
for (const m of mutants) {
  if (only.length && !only.includes(m.id)) continue;
  const file = `${root}/${m.file}`;
  const original = readFileSync(file, "utf8");
  const nl = original.includes("\r\n") ? "\r\n" : "\n";
  const from = m.from.replace(/\n/g, nl);
  if (!original.includes(from)) { appendFileSync(log, `${m.id} NOT-APPLIED ${m.note}\n`); continue; }
  writeFileSync(file, original.replace(from, m.to.replace(/\n/g, nl)));
  let result;
  try { execSync(m.cmd, { cwd: root, shell: "bash", stdio: "pipe", timeout: 600000, env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" } }); result = "SURVIVED"; }
  catch (e) { result = /error CS|error CA|error TS/.test(`${e.stdout}${e.stderr}`) ? "BUILD-ERROR" : "KILLED"; }
  finally { writeFileSync(file, original); }
  appendFileSync(log, `${m.id} ${result} restored=${readFileSync(file, "utf8") === original} ${m.note}\n`);
}
