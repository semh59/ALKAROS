import { readFileSync, writeFileSync, appendFileSync } from "node:fs";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const log = `${root}/evidence/V1-RMD-716/mutations.log`;
const dn = "~/.dotnet/dotnet.exe test";
const T = {
  ptr: `${dn} tests/Modules/Tables/CurrentPointers -nologo`,
  xfer: `${dn} tests/Modules/Tables/TableTransfer -nologo`,
  merge: `${dn} tests/Modules/Tables/TableMerge -nologo`,
  life: `${dn} tests/Modules/Tables/TableLifecycle -nologo`,
  resv: `${dn} tests/Modules/Tables/Reservations -nologo`,
  host: `${dn} tests/Host/Experience/Tables -nologo`,
  ui: "cd src/Clients/PosTerminal && export PATH=\"$PATH:/c/Program Files/nodejs\" && npx vitest run src/routes/ReservationStation.test.tsx",
};
const P = "src/Modules/Tables/CurrentPointers/PostgresTablePointerProjector.cs";
const X = "src/Modules/Tables/TableTransfer/PostgresTableTransferRepository.cs";
const U = "src/Modules/Tables/TableMerge/PostgresTableMergeRepository.Unmerge.cs";
const L = "src/Modules/Tables/TableLimits.cs";
const H = "src/Host/Experience/Tables/TableManagementApplication.cs";

const mutants = [
  { id: "P1", file: P, t: "ptr", from: "status NOT IN ('Completed', 'Cancelled', 'Rejected') {NotSentOrder}\n            ORDER BY created_at DESC;", to: "status NOT IN ('Completed', 'Cancelled', 'Rejected')\n            ORDER BY created_at DESC;", note: "the projector counts a check sent to the till as an open order" },
  { id: "P2", file: P, t: "ptr", from: "status NOT IN ('Paid', 'Cancelled') {NotSentBill}\n            ORDER BY opened_at DESC", to: "status NOT IN ('Paid', 'Cancelled')\n            ORDER BY opened_at DESC", note: "the projector counts a sent check's bill as open" },
  { id: "P3", file: P, t: "ptr", from: 'else if (currentStatus == "Reserved" && pendingQrOrderId is not null)', to: 'else if (currentStatus == "Reserved" && pendingQrOrderId is not null && currentStatus == "x")', note: "a table a QR order holds Reserved is projected Occupied" },
  { id: "P4", file: P, t: "ptr", from: "(drift.DriftTypes & ~TablePointerDriftType.StatusMismatch) == TablePointerDriftType.None", to: "drift.DriftTypes == TablePointerDriftType.None", note: "repair also acts on a status mismatch alone" },
  { id: "P5", file: P, t: "ptr", from: "if ((await RebuildTablePointersAsync(drift.TableId, cancellationToken)).WasModified)\n                repaired++;", to: "await RebuildTablePointersAsync(drift.TableId, cancellationToken);", note: "repair counts nothing" },
  { id: "X1", file: X, t: "xfer", from: "{NotSentOrder}\n            ORDER BY created_at DESC;", to: "{NotSentOrder}\n            ORDER BY created_at ASC;", note: "transfer picks the oldest order as the target pointer" },
  { id: "X2", file: X, t: "xfer", from: "ORDER BY opened_at DESC, created_at DESC;", to: "ORDER BY opened_at ASC, created_at ASC;", note: "transfer picks the oldest bill as the target pointer" },
  { id: "U1", file: U, t: "merge", from: "expectedVersionMap[mergeRecord.MergedTableId] = actualVersion;", to: "expectedVersionMap[mergeRecord.MergedTableId] = 0;", note: "an unmerge without a participant version fails" },
  { id: "U2", file: U, t: "merge", from: "else if (actualVersion != expectedVersion)", to: "else if (false)", note: "a stale participant version no longer guards the unmerge" },
  { id: "L1", file: L, t: "life", from: "reason.Length > MaxReasonLength", to: "reason.Length > MaxReasonLength + 1000", note: "a reason of any length is accepted" },
  { id: "L2", file: "src/Modules/Tables/Reservations/TableReservationRequests.cs", t: "life", from: "PartySize is <= 0 or > TableLimits.MaxPartySize", to: "PartySize <= 0", note: "party size has no upper bound" },
  { id: "L3", file: "src/Modules/Tables/TableMerge/TableMergeRequest.cs", t: "life", from: "Participants.Count > TableLimits.MaxMergeParticipants", to: "Participants.Count > 1000", note: "a merge has no participant bound" },
  { id: "L4", file: "src/Modules/Tables/TableLifecycle/Table.cs", t: "life", from: "capacity is < 0 or > TableLimits.MaxCapacity", to: "capacity < 0", note: "capacity has no upper bound" },
  { id: "R1", file: "src/Modules/Tables/Reservations/PostgresTableReservationRepository.cs", t: "resv", from: "WHERE table_id = ANY(@table_ids) AND status = 'Active';", to: "WHERE table_id = ANY(@table_ids);", note: "the batch lookup returns a released reservation" },
  { id: "H1", file: "src/Host/Experience/Tables/TableManagementContracts.cs", t: "host", from: '"Reserve", "Merge", "SetOutOfService"', to: '"Reserve", "SetOutOfService"', note: "a free table cannot be merged" },
  { id: "H2", file: H, t: "host", from: '"ux_tables_zone_number" => "Bu bölgede aynı numaralı bir masa zaten var."', to: '"ux_tables_zone_number" => "Aynı kayıt zaten var."', note: "a duplicate table number gets the generic message" },
  { id: "H3", file: H, t: "host", from: "request.Participants is null ? [] : ", to: "", note: "an unmerge request without participants is rejected" },
  { id: "H4", file: H, t: "host", from: "        services.AddHostedService<TablePointerRepairHostedService>();\n", to: "", note: "the pointer repair job is not registered" },
  { id: "H5", file: H, t: "host", from: "activeReservations.TryGetValue(table.Id, out var activeReservation);", to: "TableReservationRecord? activeReservation = null;", note: "the table list drops every active reservation" },
  { id: "C1", file: "src/Clients/PosTerminal/src/routes/ReservationStation.tsx", t: "ui", from: ': "unreachable");', to: ': "anonymous");', note: "an unreachable server shows the login form" },
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
  try { execSync(T[m.t], { cwd: root, shell: "bash", stdio: "pipe", timeout: 600000, env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" } }); result = "SURVIVED"; }
  catch (e) { result = /error CS|error CA/.test(`${e.stdout}${e.stderr}`) ? "BUILD-ERROR" : "KILLED"; }
  finally { writeFileSync(file, original); }
  appendFileSync(log, `${m.id} ${result} restored=${readFileSync(file, "utf8") === original} ${m.note}\n`);
}
