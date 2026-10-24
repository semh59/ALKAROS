import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const p = (rel) => `${root}/${rel}`;
const mig = p("database/migrations/V1/V1-RMD-714/205-table-constraints-and-sent-checks.up.sql");
const mergeMain = p("src/Modules/Tables/TableMerge/PostgresTableMergeRepository.Merge.cs");
const unmerge = p("src/Modules/Tables/TableMerge/PostgresTableMergeRepository.Unmerge.cs");
const transfer = p("src/Modules/Tables/TableTransfer/PostgresTableTransferRepository.cs");
const handoff = p("src/Host/Experience/Orders/CashierHandoffStore.cs");
const store = p("src/Host/Experience/Tables/TableManagementStore.cs");
const log = p("evidence/V1-RMD-714/mutations.log");
const env = "ALKAROS_TEST_PG_PORT=55432 ALKAROS_TEST_PG_PASSWORD=postgres ~/.dotnet/dotnet.exe test";
const merges = `${env} tests/Modules/Tables/TableMerge/ALKAROS.Tables.TableMerge.Tests.csproj`;
const transfers = `${env} tests/Modules/Tables/TableTransfer/ALKAROS.Tables.TableTransfer.Tests.csproj`;
const reservations = `${env} tests/Modules/Tables/Reservations/ALKAROS.Tables.Reservations.Tests.csproj`;
const host = `${env} tests/Host/Experience/Tables/ALKAROS.Host.Experience.Tables.Tests.csproj --filter "FullyQualifiedName~TableServerRulesHttpTests"`;

const mutants = [
  { id: "D1", file: mig, from: "ON table_mgmt.table_reservations (table_id)\n    WHERE status = 'Active';", to: "ON table_mgmt.table_reservations (table_id)\n    WHERE status = 'Nothing';", cmd: reservations, note: "no unique active reservation per table" },
  { id: "D2", file: mig, from: "ON table_mgmt.table_merges (merged_table_id)\n    WHERE status = 'Active';", to: "ON table_mgmt.table_merges (merged_table_id)\n    WHERE status = 'Nothing';", cmd: merges, note: "no unique active merge per participant" },
  { id: "D3", file: mig, from: "CHECK (party_size >= 1)", to: "CHECK (party_size >= 0)", cmd: reservations, note: "party size zero allowed" },
  { id: "D4", file: mig, from: "CHECK (expires_at IS NULL OR expires_at > reserved_at)", to: "CHECK (true)", cmd: reservations, note: "reservation may end before it starts" },
  { id: "D5", file: mig, from: "CHECK (primary_table_id <> merged_table_id)", to: "CHECK (true)", cmd: merges, note: "a table may merge with itself" },
  { id: "R1", file: mergeMain, from: "WHERE table_id = @part_id AND status NOT IN ('Completed', 'Cancelled', 'Rejected') {NotSentOrder};", to: "WHERE table_id = @part_id AND status NOT IN ('Completed', 'Cancelled', 'Rejected');", cmd: merges, note: "merge moves a participant's sent check" },
  { id: "R2", file: mergeMain, from: "WHERE table_id = @primary_id AND status NOT IN ('Completed', 'Cancelled', 'Rejected') {NotSentOrder};", to: "WHERE table_id = @primary_id AND status NOT IN ('Completed', 'Cancelled', 'Rejected');", cmd: merges, note: "merge counts the primary's sent check" },
  { id: "R2b", file: mergeMain, from: "WHERE table_id = @primary_id AND status NOT IN ('Paid', 'Cancelled') {NotSentBill};", to: "WHERE table_id = @primary_id AND status NOT IN ('Paid', 'Cancelled');", cmd: merges, note: "merge counts the primary's sent bill" },
  { id: "R3", file: mergeMain, from: "WHERE table_id = ANY(@table_ids) AND status NOT IN ('Paid', 'Cancelled') {NotSentBill};", to: "WHERE table_id = ANY(@table_ids) AND status NOT IN ('Paid', 'Cancelled');", cmd: merges, note: "a sent check's pending payment blocks a merge" },
  { id: "R4", file: mergeMain, from: "WHERE table_id = @part_id AND status NOT IN ('Paid', 'Cancelled') {NotSentBill};", to: "WHERE table_id = @part_id AND status NOT IN ('Paid', 'Cancelled');", cmd: merges, note: "merge moves a participant's sent bill" },
  { id: "R5", file: unmerge, from: "AND status NOT IN ('Completed', 'Cancelled', 'Rejected') {NotSentOrder}\n              AND NOT (order_id", to: "AND status NOT IN ('Completed', 'Cancelled', 'Rejected')\n              AND NOT (order_id", cmd: merges, note: "unmerge points the primary at a sent check" },
  { id: "R6", file: unmerge, from: "AND status NOT IN ('Paid', 'Cancelled') {NotSentBill}\n              AND NOT (bill_id", to: "AND status NOT IN ('Paid', 'Cancelled')\n              AND NOT (bill_id", cmd: merges, note: "unmerge points the primary at a sent bill" },
  { id: "R7", file: transfer, from: "status NOT IN ('Completed', 'Cancelled', 'Rejected') {NotSentOrder}", to: "status NOT IN ('Completed', 'Cancelled', 'Rejected')", cmd: transfers, note: "transfer moves a sent check" },
  { id: "R8", file: transfer, from: "status NOT IN ('Paid', 'Cancelled') {NotSentBill};", to: "status NOT IN ('Paid', 'Cancelled');", cmd: transfers, note: "transfer moves a sent bill" },
  { id: "H1", file: handoff, from: "            await mark.ExecuteNonQueryAsync(cancellationToken);\n", to: "", cmd: host, note: "sending a check records nothing" },
  { id: "H2", file: handoff, from: "        }\n\n        await ForgetSentCheckAsync(connection, transaction, orderId, cancellationToken);\n", to: "        }\n", cmd: host, note: "recall keeps the sent record" },
  { id: "H3", file: handoff, from: "            await ForgetSentCheckAsync(connection, transaction, orderId, cancellationToken);\n            await transaction.CommitAsync", to: "            await transaction.CommitAsync", cmd: host, note: "repeated recall keeps the sent record" },
  { id: "T1", file: store, from: "WHERE m.merged_table_id = t.table_id AND m.status = 'Active'),", to: "WHERE m.merged_table_id = t.table_id AND m.status = 'Nothing'),", cmd: host, note: "merged tables show only their own total" },
  { id: "T2", file: store, from: "              AND NOT EXISTS (SELECT 1 FROM table_mgmt.table_sent_checks sc WHERE sc.order_id = o.order_id)\n", to: "", cmd: host, note: "total counts sent checks" },
  { id: "T3", file: store, from: "              AND o.status NOT IN ('Completed', 'Cancelled', 'Rejected')\n", to: "", cmd: host, note: "total counts closed orders" },
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
