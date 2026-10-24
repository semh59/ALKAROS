import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const p = (rel) => `${root}/${rel}`;
const merge = p("src/Modules/Tables/TableMerge/PostgresTableMergeRepository.Merge.cs");
const unmerge = p("src/Modules/Tables/TableMerge/PostgresTableMergeRepository.Unmerge.cs");
const orderRepo = p("src/Modules/Orders/OrderAggregate/PostgresOrderRepository.cs");
const orderConsumer = p("src/Modules/Orders/Integration/TableEventOrderConsumer.cs");
const billConsumer = p("src/Modules/Billing/Integration/TableEventBillConsumer.cs");
const settlement = p("src/Host/Experience/Orders/OrderSettlementService.cs");
const contracts = p("src/Host/Experience/Tables/TableManagementContracts.cs");
const floorUi = p("src/Clients/PosTerminal/src/features/tables/FloorPlanWorkspace.tsx");
const log = p("evidence/V1-RMD-710/mutations.log");
const env = "ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres ~/.dotnet/dotnet.exe test";
const mod = `${env} tests/Modules/Tables/TableMerge/ALKAROS.Tables.TableMerge.Tests.csproj`;
const host = `${env} tests/Host/Experience/Tables/ALKAROS.Host.Experience.Tables.Tests.csproj --filter "FullyQualifiedName~MergeSettlementTests|FullyQualifiedName~FloorPlanHttpTests"`;
const web = 'cd src/Clients/PosTerminal && export PATH="$PATH:/c/Program Files/nodejs" && npx vitest run src/features/tables';

const mutants = [
  { id: "M1", file: merge, from: ": participantOrderIds.Count > 0 ? participantOrderIds[0] : null;", to: ": participantOrderIds.FirstOrDefault();", cmd: mod, note: "empty participant records an empty order id" },
  { id: "M2", file: merge, from: "allConsolidatedOrderIds.Cast<Guid?>().FirstOrDefault()", to: "allConsolidatedOrderIds.FirstOrDefault()", cmd: mod, note: "empty primary points at an empty order id" },
  { id: "M3", file: merge, from: "{ Value = participantOrderIds.ToArray() }", to: "{ Value = Array.Empty<Guid>() }", cmd: mod, note: "merge record keeps no order list" },
  { id: "M4", file: unmerge, from: "SelectOpenOrdersSql, mergeRecord.OriginalOrderIds, connection", to: "SelectOpenOrdersSql, mergeRecord.OriginalOrderId is { } single ? [single] : [], connection", cmd: mod, note: "unmerge hands back one order only" },
  { id: "M5", file: unmerge, from: "AND NOT (order_id = ANY(@restored_ids))", to: "AND (order_id = ANY(@restored_ids) OR true)", cmd: mod, note: "primary pointer adopts a returned order" },
  { id: "M6", file: unmerge, from: "WHERE order_id = ANY(@ids) AND status NOT IN ('Completed', 'Cancelled', 'Rejected')", to: "WHERE order_id = ANY(@ids)", cmd: mod, note: "paid order comes back on unmerge" },
  { id: "M7", file: merge, from: "WHERE table_id = @part_id AND status NOT IN ('Completed', 'Cancelled', 'Rejected');", to: "WHERE table_id = @part_id AND status NOT IN ('Completed', 'Cancelled');", cmd: mod, note: "rejected order recorded as open work" },
  { id: "M8", file: orderRepo, from: "WHERE table_id = @from_table_id AND status NOT IN ('Completed', 'Cancelled', 'Rejected');", to: "WHERE table_id = @from_table_id AND status NOT IN ('Completed', 'Cancelled');", cmd: mod, note: "rejected order follows the merge" },
  { id: "M9", file: orderConsumer, from: "e.OrderIds ?? (e.OriginalOrderId is { } orderId ? [orderId] : [])", to: "(e.OriginalOrderId is { } orderId ? [orderId] : [])", cmd: mod, note: "order consumer ignores the id list" },
  { id: "M9b", file: orderConsumer, from: "foreach (var id in orderIds)", to: "foreach (var id in orderIds.Take(1))", cmd: mod, note: "order consumer moves one order only (faithful rerun of M9)" },
  { id: "M11b", file: billConsumer, from: "foreach (var id in billIds)", to: "foreach (var id in billIds.Take(1))", cmd: mod, note: "bill consumer moves one bill only (faithful rerun of M11)" },
  { id: "M10", file: orderConsumer, from: "e.OrderIds ?? (e.OriginalOrderId is { } orderId ? [orderId] : [])", to: "e.OrderIds ?? []", cmd: mod, note: "order consumer drops legacy events" },
  { id: "M11", file: billConsumer, from: "e.BillIds ?? (e.OriginalBillId is { } billId ? [billId] : [])", to: "(e.OriginalBillId is { } billId ? [billId] : [])", cmd: mod, note: "bill consumer ignores the id list" },
  { id: "M12", file: settlement, from: "if (nextOrderId is { } followingOrderId)", to: "if (nextOrderId is { } followingOrderId && false)", cmd: host, note: "paying the pointer order frees a table that still has open orders" },
  { id: "M13", file: settlement, from: "WHERE primary_table_id = @table_id AND status = 'Active'", to: "WHERE primary_table_id = @table_id AND status = 'Never'", cmd: host, note: "merge stays active after the last payment" },
  { id: "M14", file: contracts, from: "if (mergePrimary && table.Active)", to: "if (false && mergePrimary && table.Active)", cmd: host, note: "server never offers unmerge" },
  { id: "M15", file: contracts, from: "                permissions,\n                table.IsMergePrimary)))", to: "                permissions,\n                false)))", cmd: host, note: "floor plan does not pass the merge leader flag" },
  { id: "C1", file: floorUi, from: ".filter((command): command is TableAction => command in tableActionLabels)", to: ".filter((command): command is TableAction => command !== \"Unmerge\" && command in tableActionLabels)", cmd: web, note: "unmerge button hidden even when offered" },
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
