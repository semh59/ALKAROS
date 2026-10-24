import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const p = (rel) => `${root}/${rel}`;
const merge = p("src/Modules/Tables/TableMerge/PostgresTableMergeRepository.Merge.cs");
const transfer = p("src/Modules/Tables/TableTransfer/PostgresTableTransferRepository.cs");
const orderConsumer = p("src/Modules/Orders/Integration/TableEventOrderConsumer.cs");
const billConsumer = p("src/Modules/Billing/Integration/TableEventBillConsumer.cs");
const store = p("src/Host/Experience/Tables/TableManagementStore.cs");
const app = p("src/Host/Experience/Tables/TableManagementApplication.cs");
const trigger = p("database/migrations/V1/V1-RMD-711/204-table-status-changed-at-trigger.up.sql");
const log = p("evidence/V1-RMD-711/mutations.log");
const env = "ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres ~/.dotnet/dotnet.exe test";
const merges = `${env} tests/Modules/Tables/TableMerge/ALKAROS.Tables.TableMerge.Tests.csproj`;
const transfers = `${env} tests/Modules/Tables/TableTransfer/ALKAROS.Tables.TableTransfer.Tests.csproj`;
const host = `${env} tests/Host/Experience/Tables/ALKAROS.Host.Experience.Tables.Tests.csproj --filter "FullyQualifiedName~TableGuardsHttpTests"`;

const mutants = [
  { id: "N1", file: merge, from: "participantOrderIds, participantBillIds))),", to: "null, null))),", cmd: merges, note: "merge event names no orders" },
  { id: "N2", file: transfer, from: "activeOrderIds, activeBillIds))),", to: "null, null))),", cmd: transfers, note: "transfer event names no orders" },
  { id: "N3", file: orderConsumer, from: "                if (!await IsMergeActiveAsync(e.MergeGroupId, e.ParticipantTableId, connection, transaction, cancellationToken).ConfigureAwait(false))\n                    break;\n", to: "", cmd: merges, note: "order consumer applies a merge that is no longer active" },
  { id: "N4", file: billConsumer, from: "                if (!await IsMergeActiveAsync(e.MergeGroupId, e.ParticipantTableId, connection, transaction, cancellationToken).ConfigureAwait(false))\n                    break;\n", to: "", cmd: merges, note: "bill consumer applies a merge that is no longer active" },
  { id: "N5", file: orderConsumer, from: "foreach (var id in e.OrderIds)\n                {\n                    await _orders.ReparentOrderToTableAsync(\n                        id, e.SourceTableId,", to: "foreach (var id in e.OrderIds.Take(0))\n                {\n                    await _orders.ReparentOrderToTableAsync(\n                        id, e.SourceTableId,", cmd: transfers, note: "order consumer moves nothing on a transfer" },
  { id: "N6", file: transfer, from: "if (await cmd.ExecuteScalarAsync(cancellationToken) is not null)\n                throw new TableInActiveMergeException", to: "if (await cmd.ExecuteScalarAsync(cancellationToken) is not null && false)\n                throw new TableInActiveMergeException", cmd: transfers, note: "transfer ignores active merges" },
  { id: "N7", file: store, from: "if (current.State == TableState.Occupied)\n            await EnsureNoOpenWorkAsync", to: "if (false && current.State == TableState.Occupied)\n            await EnsureNoOpenWorkAsync", cmd: host, note: "status endpoint skips the open work guard" },
  { id: "N8", file: store, from: "if (await IsInActiveMergeAsync(current.Id, cancellationToken))", to: "if (false && await IsInActiveMergeAsync(current.Id, cancellationToken))", cmd: host, note: "status endpoint releases merged tables" },
  { id: "N9", file: store, from: "status NOT IN ('Draft', 'Completed', 'Cancelled', 'Rejected'))", to: "status NOT IN ('Completed', 'Cancelled', 'Rejected'))", cmd: host, note: "an empty draft blocks releasing the table" },
  { id: "N10", file: store, from: "if (existing.State is TableState.Occupied or TableState.Reserved)", to: "if (existing.State is TableState.Reserved)", cmd: host, note: "occupied table can be switched off" },
  { id: "N11", file: app, from: "\"tables.table.status_changed\"", to: "\"tables.table.changed\"", cmd: host, note: "status change audit renamed" },
  { id: "N12", file: app, from: "blocked => (409, blocked.Code, blocked.Message)", to: "blocked => (409, \"DOMAIN_CONFLICT\", blocked.Message)", cmd: host, note: "blocked operation loses its code" },
  { id: "N13", file: trigger, from: "IF NEW.current_status IS DISTINCT FROM OLD.current_status THEN", to: "IF true THEN", cmd: host, note: "timestamp moves on every update" },
  { id: "N14", file: app, from: "\"tables.floor_plan.saved\"", to: "\"tables.floor_plan.stored\"", cmd: host, note: "floor plan audit renamed" },
  { id: "N15", file: app, from: "\"tables.zone.created\"", to: "\"tables.zone.made\"", cmd: host, note: "zone creation audit renamed" },
];

const only = process.argv.slice(2);
for (const m of mutants) {
  if (only.length && !only.includes(m.id)) continue;
  const original = readFileSync(m.file, "utf8");
  const nl = original.includes("\r\n") ? "\r\n" : "\n";
  const from = m.from.replace(/\n/g, nl);
  if (!original.includes(from)) { appendFileSync(log, `${m.id} NOT-APPLIED ${m.note}\n`); continue; }
  writeFileSync(m.file, original.replace(from, m.to.replace(/\n/g, nl)));
  let result;
  try { execSync(m.cmd, { cwd: root, shell: "bash", stdio: "pipe", timeout: 600000 }); result = "SURVIVED"; }
  catch (e) { result = /error CS|error CA|error TS/.test(`${e.stdout}${e.stderr}`) ? "BUILD-ERROR" : "KILLED"; }
  finally { writeFileSync(m.file, original); }
  appendFileSync(log, `${m.id} ${result} restored=${readFileSync(m.file, "utf8") === original} ${m.note}\n`);
}
