import fs from 'node:fs';
import { execSync } from 'node:child_process';
const ST = 'src/Host/Experience/Billing/BillingSplitStore.cs';
const APP = 'src/Host/Experience/Billing/BillingSplitApplication.cs';
const dn = `${process.env.HOME}/.dotnet/dotnet.exe`;
const T = (f) => `${dn} test tests/Host/Experience/Billing -nologo --filter "${f}"`;
const KEYS = 'FullyQualifiedName~ADiscountOrTipWithoutAKey';
const SAME = 'FullyQualifiedName~TheSameKeyWithAnotherBody';
// [id, file, [[from, to, all?]...], test command]
const M = [
  ['M1-blank-key-accepted', ST, [['string.IsNullOrWhiteSpace(idempotencyKey) ||', 'idempotencyKey is null ||']], T(KEYS)],
  ['M2-discount-replay-body-unchecked', ST, [['if (!SameDiscount(replay, request))', 'if (false)']], T(SAME)],
  ['M3-tip-replay-amount-unchecked', ST, [['replay.Amount != BillMath.RoundCurrency(request.Amount) ||', 'false ||']], T(SAME)],
  ['M4-tip-above-limit-accepted', ST, [['request.Amount <= 0 || request.Amount > MaxAdjustmentAmount', 'request.Amount <= 0']], T(KEYS)],
  ['M5-percentage-cap-removed', ST, [['"Percentage" => 100m,', '"Percentage" => 1000000m,']], T('FullyQualifiedName~ADiscountRequestForAMissingBill')],
  ['M6-notes-limit-removed', ST, [['notes is { Length: > MaxNotesLength }', 'false']], T(KEYS)],
  ['M7-row-version-not-bumped', ST, [['await BumpBillRowVersionAsync(connection, lockTransaction, billId, cancellationToken);', '', true]], T('FullyQualifiedName~ADiscountOrTipRefusesASplit')],
  ['M8-approver-not-recorded', ST, [['approverId ?? actorId', 'actorId', true]], T('FullyQualifiedName~AGrantApprovedDiscountRecords')],
  ['M9-grant-opened-without-prevalidation', APP, [['await store.EnsureDiscountRequestableAsync(billId, request, cancellationToken);', '']], T('FullyQualifiedName~ADiscountRequestForAMissingBill')],
  ['M10-discount-status-read-before-lock', ST, [
    ['        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);\n        await using var lockTransaction = await connection.BeginTransactionAsync(cancellationToken);\n        await using (var lockCommand = new NpgsqlCommand(\n            "SELECT bill_id FROM billing.bills WHERE bill_id = @bill_id FOR UPDATE;", connection, lockTransaction))\n        {\n            lockCommand.Parameters.AddWithValue("bill_id", billId);\n            await lockCommand.ExecuteScalarAsync(cancellationToken);\n        }\n\n        // Read only after the lock',
     '        var preBill = await _bills.GetByIdAsync(billId, cancellationToken) ?? throw new BillingSplitNotFoundException("x");\n        if (!IsDiscountable(preBill.Status)) throw new BillDiscountUnsupportedBillStateException(billId, preBill.Status.ToString());\n        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);\n        await using var lockTransaction = await connection.BeginTransactionAsync(cancellationToken);\n        await using (var lockCommand = new NpgsqlCommand(\n            "SELECT bill_id FROM billing.bills WHERE bill_id = @bill_id FOR UPDATE;", connection, lockTransaction))\n        {\n            lockCommand.Parameters.AddWithValue("bill_id", billId);\n            await lockCommand.ExecuteScalarAsync(cancellationToken);\n        }\n\n        // Read only after the lock'],
    ['        // Found by an independent audit (2026-09-06): nothing stopped a discount from being applied to a\n        // Paid/PartiallyPaid/Cancelled bill; same mutable-state set SplitDesign enforces.\n        if (!IsDiscountable(bill.Status))\n            throw new BillDiscountUnsupportedBillStateException(billId, bill.Status.ToString());\n', ''],
  ], T('FullyQualifiedName~TheBillStateIsReadOnlyAfterTheLock')],
];
const only = process.argv[2];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres' };
const log = 'evidence/V1-RMD-729/mutations.log';
for (const [id, file, edits, cmd] of M) {
  if (only && only !== id) continue;
  const orig = fs.readFileSync(file, 'utf8');
  let mutated = orig; let ok = true;
  for (const [from, to, all] of edits) {
    if (!mutated.includes(from)) { ok = false; break; }
    mutated = all ? mutated.split(from).join(to) : mutated.replace(from, to);
  }
  if (!ok) { fs.appendFileSync(log, `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, mutated);
  let verdict;
  try { execSync(cmd, { stdio: 'pipe', env, timeout: 600000 }); verdict = 'SURVIVED'; }
  catch (e) { verdict = /error CS|error CA/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync(log, `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync(log, 'DONE\n');
