import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const file = `${root}/tests/Architecture/ModuleBoundaries/ModuleBoundaryTests.cs`;
const log = `${root}/evidence/V1-RMD-715/mutations.log`;
const cmd = "~/.dotnet/dotnet.exe test tests/Architecture/ModuleBoundaries/ALKAROS.Architecture.Tests.csproj";

const mutants = [
  { id: "A1", from: '        "src/Modules/Tables/TableTransfer/PostgresTableTransferRepository.cs",\n', to: "", note: "a Tables file with a raw audit insert is missing from the list" },
  { id: "A2", from: '        "src/Modules/Orders/ItemExceptions/ItemExceptionHandler.cs",\n', to: "", note: "an Orders file with a raw audit insert is missing from the list" },
  { id: "A3", from: "|\\{\\w*[Aa]udit\\w*\\}", to: "", note: "the pattern ignores an audit-named constant" },
  { id: "A4", from: ' | System.Text.RegularExpressions.RegexOptions.CultureInvariant', to: "", note: "the pattern depends on the current culture" },
  { id: "A5", from: ".Where(f => RawAuditInsert.IsMatch(File.ReadAllText(f)))", to: ".Where(f => false)", note: "the scan finds nothing" },
];

const only = process.argv.slice(2);
for (const m of mutants) {
  if (only.length && !only.includes(m.id)) continue;
  const original = readFileSync(file, "utf8");
  const nl = original.includes("\r\n") ? "\r\n" : "\n";
  const from = m.from.replace(/\n/g, nl);
  if (!original.includes(from)) { appendFileSync(log, `${m.id} NOT-APPLIED ${m.note}\n`); continue; }
  writeFileSync(file, original.replace(from, m.to));
  let result;
  try { execSync(cmd, { cwd: root, shell: "bash", stdio: "pipe", timeout: 600000, env: { ...process.env } }); result = "SURVIVED"; }
  catch (e) { result = /error CS|error CA/.test(`${e.stdout}${e.stderr}`) ? "BUILD-ERROR" : "KILLED"; }
  finally { writeFileSync(file, original); }
  appendFileSync(log, `${m.id} ${result} restored=${readFileSync(file, "utf8") === original} ${m.note}\n`);
}
