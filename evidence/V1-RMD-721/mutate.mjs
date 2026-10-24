import fs from 'node:fs';
import { execSync } from 'node:child_process';
const ST = 'src/Host/Experience/NfcOrdering/NfcOrderingStore.cs';
const EX = 'src/Host/Experience/Orders/PendingOrderConfirmation/QrOrderExpiryHostedService.cs';
const DS = 'src/Host/DualScreen/DualScreenApplication.cs';
const UI = 'src/Clients/PosTerminal/src/routes/NfcOrder.tsx';
const NFC = 'dotnet tests/Host/Experience/NfcOrdering';
const CON = 'dotnet tests/Host/Experience/Orders/Confirmation';
const AH = 'dotnet tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj --filter FullyQualifiedName~AccessHardeningTests';
const VT = 'vitest';
const M = [
  ['N1-replay-not-compared', ST, 'if (!sent.SequenceEqual(stored))', 'if (false)', NFC],
  ['N2-order-number-no-suffix', ST, '-{orderId.ToString("N")[..8]}",', '",', NFC],
  ['N3-announce-failure-escapes', ST, 'catch (Exception exception) when (exception is not OperationCanceledException)', 'catch (Exception exception) when (exception is OperationCanceledException)', NFC],
  ['N4-no-location-escapes', ST, '            or StockItemHasNoDefaultLocationException\n', '', NFC],
  ['X1-nfc-not-expired', EX, "OR (o.source = 'Nfc' AND NOT EXISTS (", "OR (o.source = 'Nfx' AND NOT EXISTS (", CON],
  ['X2-cooking-nfc-expired', EX, "ti.status NOT IN ('Queued', 'Cancelled')", "ti.status = 'None'", CON],
  ['R1-address-limit-off', DS, 'if (policy == "nfc-order")', 'if (policy == "nfc-order-off")', AH],
  ['U1-pending-text-missing', UI, 'result.status === "PendingConfirmation"', 'result.status === "Never"', VT],
  ['U2-id-not-reset', UI, '        setSubmissionId(null);\n        setMessage("Sepetiniz', '        setMessage("Sepetiniz', VT],
];
const only = process.argv[2];
const env = { ...process.env, ALKAROS_TEST_PG_PORT: '55432', ALKAROS_TEST_PG_PASSWORD: 'postgres', PATH: process.env.PATH + ';/c/Program Files/nodejs' };
for (const [id, file, from, to, target] of M) {
  if (only && only !== id) continue;
  const orig = fs.readFileSync(file, 'utf8');
  const crlf = orig.includes('\r\n');
  const f = crlf ? from.replace(/\n/g, '\r\n') : from;
  const t = crlf ? to.replace(/\n/g, '\r\n') : to;
  if (!orig.includes(f)) { fs.appendFileSync('evidence/V1-RMD-721/mutations.log', `${id} NOT-APPLIED\n`); continue; }
  fs.writeFileSync(file, orig.replace(f, t));
  let verdict;
  try {
    if (target === VT) execSync('npx vitest run src/routes/NfcOrder', { cwd: 'src/Clients/PosTerminal', stdio: 'pipe', env });
    else execSync(`${process.env.HOME}/.dotnet/dotnet.exe test ${target.replace('dotnet ', '')} -nologo`, { stdio: 'pipe', env });
    verdict = 'SURVIVED';
  } catch (e) { verdict = /error CS|error CA/.test(String(e.stdout)) ? 'BUILD-ERROR' : 'KILLED'; }
  fs.writeFileSync(file, orig);
  fs.appendFileSync('evidence/V1-RMD-721/mutations.log', `${id} ${verdict} restored=${fs.readFileSync(file, 'utf8') === orig}\n`);
}
fs.appendFileSync('evidence/V1-RMD-721/mutations.log', 'DONE\n');
