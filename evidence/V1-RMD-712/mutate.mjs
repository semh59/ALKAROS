import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const p = (rel) => `${root}/${rel}`;
const repo = p("src/Modules/Tables/Reservations/PostgresTableReservationRepository.cs");
const app = p("src/Host/Experience/Tables/TableManagementApplication.cs");
const contracts = p("src/Host/Experience/Tables/TableManagementContracts.cs");
const store = p("src/Host/Experience/Tables/TableManagementStore.cs");
const log = p("evidence/V1-RMD-712/mutations.log");
const env = "ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres ~/.dotnet/dotnet.exe test";
const module = `${env} tests/Modules/Tables/Reservations/ALKAROS.Tables.Reservations.Tests.csproj`;
const rules = `${env} tests/Host/Experience/Tables/ALKAROS.Host.Experience.Tables.Tests.csproj --filter "FullyQualifiedName~TableServerRulesHttpTests"`;
const wiring = `${env} tests/Host/Experience/Tables/ALKAROS.Host.Experience.Tables.Tests.csproj --filter "FullyQualifiedName~TableManagementRegistrationTests"`;

const endArg = "                null), cancellationToken));";
const timeMutant = (id, nth, field, note) => ({ id, file: app, from: endArg, nth, to: `                request.${field}), cancellationToken));`, cmd: rules, note });

const mutants = [
  { id: "S1", file: app, from: "                null,\n                request.ExpiresAt), cancellationToken);", to: "                request.ReservedAt,\n                request.ExpiresAt), cancellationToken);", cmd: rules, note: "reservation time taken from the client" },
  timeMutant("S2", 1, "ClaimedAt", "claim time taken from the client"),
  timeMutant("S3", 2, "CancelledAt", "cancel time taken from the client"),
  timeMutant("S4", 3, "ExpiredAt", "expire time taken from the client"),
  timeMutant("S5", 4, "TransferredAt", "transfer time taken from the client"),
  timeMutant("S6", 5, "MergedAt", "merge time taken from the client"),
  timeMutant("S7", 6, "UnmergedAt", "unmerge time taken from the client"),
  { id: "S8", file: repo, from: "        await EnsureOrderBelongsToTableAsync(request.OrderId, request.TableId, connection, transaction, cancellationToken);\n", to: "", cmd: module, note: "create skips the order ownership check" },
  { id: "S9", file: repo, from: "        await EnsureOrderBelongsToTableAsync(request.OrderId, tableId, connection, transaction, cancellationToken);\n", to: "", cmd: module, note: "claim skips the order ownership check" },
  { id: "S10", file: repo, from: "if (reader.IsDBNull(3) || reader.GetFieldValue<DateTimeOffset>(3) > now)", to: "if (false)", cmd: module, note: "manual expiry works before the end time" },
  { id: "S11", file: repo, from: "WHERE r.status = 'Active' AND r.expires_at <= @now", to: "WHERE r.status = 'Active' AND r.expires_at > @now", cmd: module, note: "sweep picks the not yet due reservations" },
  { id: "S12", file: repo, from: "if (request.ExpiresAt <= now)", to: "if (false)", cmd: rules, note: "an end time in the past is accepted" },
  { id: "S13", file: contracts, from: 'TableState.Occupied => ["Update", "SetAvailable", "Transfer", "Merge"],', to: 'TableState.Occupied => ["Update", "SetAvailable", "Reserve", "Transfer", "Merge"],', cmd: rules, note: "occupied table is offered Reserve" },
  { id: "S14", file: store, from: "              AND t.table_id <> '00000000-0000-0000-0000-000000000001'\n", to: "", cmd: rules, note: "quick-sale anchor listed as a floor table" },
  { id: "S15", file: app, from: '(409, "RESERVATION_NOT_DUE",', to: '(409, "DOMAIN_CONFLICT",', cmd: rules, note: "not-due refusal loses its code" },
  { id: "S16", file: app, from: "        services.AddHostedService<ReservationExpiryHostedService>();\n", to: "", cmd: wiring, note: "expiry worker not registered" },
];

const only = process.argv.slice(2);
for (const m of mutants) {
  if (only.length && !only.includes(m.id)) continue;
  const original = readFileSync(m.file, "utf8");
  const nl = original.includes("\r\n") ? "\r\n" : "\n";
  const from = m.from.replace(/\n/g, nl);
  const to = m.to.replace(/\n/g, nl);
  let at = -1;
  for (let i = 0; i < (m.nth ?? 1); i++) at = original.indexOf(from, at + 1);
  if (at < 0) { appendFileSync(log, `${m.id} NOT-APPLIED ${m.note}\n`); continue; }
  writeFileSync(m.file, original.slice(0, at) + to + original.slice(at + from.length));
  let result;
  try { execSync(m.cmd, { cwd: root, shell: "bash", stdio: "pipe", timeout: 600000 }); result = "SURVIVED"; }
  catch (e) { result = /error CS|error CA|error TS/.test(`${e.stdout}${e.stderr}`) ? "BUILD-ERROR" : "KILLED"; }
  finally { writeFileSync(m.file, original); }
  appendFileSync(log, `${m.id} ${result} restored=${readFileSync(m.file, "utf8") === original} ${m.note}\n`);
}
