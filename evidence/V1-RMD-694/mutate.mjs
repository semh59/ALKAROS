import fs from "node:fs";
import path from "node:path";
import { execSync } from "node:child_process";

const root = "D:/PROJECT/ALKAROS";
const payments = "src/Host/DualScreen/DualScreenApplication.Payments.cs";
const cashApp = "src/Host/DualScreen/DualScreenApplication.CashSession.cs";
const receipt = "src/Host/DualScreen/DualScreenApplication.Receipt.cs";
const ordersStore = "src/Host/DualScreen/DualScreenStore.Orders.cs";
const display = "src/Host/DualScreen/DualScreenStore.Display.cs";
const sessionRepo = "src/Modules/Cash/SessionLifecycle/PostgresCashSessionRepository.cs";
const ledgerRepo = "src/Modules/Cash/TransactionLedger/PostgresCashTransactionLedgerRepository.cs";
const suites = {
  pay: { project: "tests/Host/Experience/PaymentTender/ALKAROS.Host.Experience.PaymentTender.Tests.csproj", filter: null },
  cash: { project: "tests/Host/Experience/CashSession/ALKAROS.Host.Experience.CashSession.Tests.csproj", filter: null },
  led: { project: "tests/Modules/Cash/TransactionLedger/ALKAROS.Cash.TransactionLedger.Tests.csproj", filter: null },
  life: { project: "tests/Modules/Cash/SessionLifecycle/ALKAROS.Cash.SessionLifecycle.Tests.csproj", filter: null },
  st: { project: "tests/Host/MigrationComposition/ALKAROS.Host.Tests.csproj", filter: "FullyQualifiedName~DualScreenStoreTests" },
};
const mutants = [
  { id: "L1", suite: "pay", file: payments, from: "            CloseFailedLog(logger, billId, exception);\n", to: "" },
  { id: "L2", suite: "pay", file: payments, from: "                    OrderNotCompletedLog(logger, billId, exception);\n", to: "" },
  { id: "L3", suite: "pay", file: payments, from: "                    DisplayPushFailedLog(logger, billId, exception);\n", to: "" },
  { id: "L4", suite: "pay", file: payments, from: "            CloseFailedLog(logger, billId, exception);\n            return false;", to: "            CloseFailedLog(logger, billId, exception);\n            return true;" },
  { id: "A1", suite: "life", file: sessionRepo, from: "AND status IN ('Open', 'Counting', 'Closing');", to: "AND status IN ('Open', 'Closing');" },
  { id: "A2", suite: "life", file: sessionRepo, from: "AND status IN ('Open', 'Counting', 'Closing');", to: "AND status IN ('Open', 'Counting');" },
  { id: "A3", suite: "life", file: sessionRepo, from: "AND status IN ('Open', 'Counting', 'Closing');", to: "AND status IN ('Open', 'Counting', 'Closing', 'Closed');" },
  { id: "K1", suite: "led", file: ledgerRepo, from: "FROM cash.cash_sessions WHERE cash_session_id = @id FOR SHARE;", to: "FROM cash.cash_sessions WHERE cash_session_id = @id;" },
  { id: "K2", suite: "led", file: ledgerRepo, from: "if (parsed != CashSessionStatus.Open)", to: "if (parsed == CashSessionStatus.Open)" },
  { id: "K3", suite: "led", file: ledgerRepo, from: "CashTransactionType.Sale or CashTransactionType.CashIn or CashTransactionType.CashOut)", to: "CashTransactionType.Sale or CashTransactionType.CashIn)" },
  { id: "K4", suite: "led", file: ledgerRepo, from: "CashTransactionType.Sale or CashTransactionType.CashIn or CashTransactionType.CashOut)", to: "CashTransactionType.CashIn or CashTransactionType.CashOut)" },
  { id: "K5", suite: "led", file: ledgerRepo, from: "?? throw new CashSessionNotFoundException(cashSessionId);", to: "?? \"Open\";" },
  { id: "R1", suite: "cash", file: cashApp, from: "if (session is null || session.Snapshot.TerminalId != terminalId)", to: "if (session is not null && session.Snapshot.TerminalId != terminalId)" },
  { id: "R2", suite: "cash", file: cashApp, from: "if (recorded.Amount != request.Amount || recorded.Direction != direction)", to: "if (recorded.Direction != direction)" },
  { id: "R3", suite: "cash", file: cashApp, from: "if (recorded.Amount != request.Amount || recorded.Direction != direction)", to: "if (recorded.Amount != request.Amount)" },
  { id: "O1", suite: "st", file: ordersStore, from: "            && i.Modifiers.Count == 0\n", to: "" },
  { id: "O2", suite: "st", file: ordersStore, from: "            && string.IsNullOrWhiteSpace(i.Notes)\n", to: "" },
  { id: "O3", suite: "st", file: ordersStore, from: "            && i.SeatId is null\n", to: "" },
  { id: "O4", suite: "st", file: ordersStore, from: "            && i.CourseNumber is null\n", to: "" },
  { id: "O5", suite: "st", file: ordersStore, from: "            && i.DiscountAmount == 0m\n", to: "" },
  { id: "O6", suite: "st", file: ordersStore, from: "            && i.UnitPrice == product.UnitPrice);", to: "            );" },
  { id: "P1", suite: "st", file: ordersStore, from: "PostgresOrderRepository.GetByIdAsync(orderId, connection, transaction, cancellationToken)", to: "new PostgresOrderRepository(_dataSource).GetByIdAsync(orderId, cancellationToken)" },
  { id: "Q1", suite: "st", file: display, from: "unitPrice * quantity + modifierTotal", to: "unitPrice * quantity" },
  { id: "Q2", suite: "st", file: display, from: "round(m.price_delta * m.quantity, 2)", to: "round(m.price_delta, 2)" },
  { id: "T1", suite: "pay", file: receipt, from: "WHERE s.payment_id = p.id AND s.outcome = 'Approved') THEN 'Card'", to: "WHERE s.payment_id = p.id AND s.outcome = 'Declined') THEN 'Card'" },
];
const only = process.argv[2] ? process.argv[2].split(",") : null;
const log = [];
for (const m of mutants) {
  if (only && !only.includes(m.id)) continue;
  const full = path.join(root, m.file);
  const orig = fs.readFileSync(full, "utf8");
  const crlf = orig.includes("\r\n");
  const normalized = crlf ? orig.replace(/\r\n/g, "\n") : orig;
  if (!normalized.includes(m.from)) { log.push(`${m.id} NOT-APPLIED`); console.log(log.at(-1)); continue; }
  fs.writeFileSync(full, crlf ? normalized.replace(m.from, () => m.to).replace(/\n/g, "\r\n") : normalized.replace(m.from, () => m.to));
  let out;
  try {
    const suite = suites[m.suite];
    const filter = suite.filter ? ` --filter "${suite.filter}"` : "";
    out = execSync(`"${process.env.USERPROFILE}/.dotnet/dotnet.exe" test ${suite.project}${filter}`, {
      cwd: root, encoding: "utf8", stdio: ["ignore", "pipe", "pipe"], timeout: 550000,
      env: { ...process.env, ALKAROS_TEST_PG_PORT: "55432", ALKAROS_TEST_PG_PASSWORD: "postgres" },
    });
  } catch (e) { out = (e.stdout ?? "") + (e.stderr ?? "") + "\nFAILED"; }
  fs.writeFileSync(full, orig);
  const verdict = /error CS|error CA/.test(out) ? "BUILD-ERROR" : /Başarısız!|FAILED/.test(out) ? "KILLED" : "SURVIVED";
  log.push(`${m.id} ${verdict} restored=${fs.readFileSync(full, "utf8") === orig}`);
  console.log(log.at(-1));
}
fs.appendFileSync(path.join(root, "evidence/V1-RMD-694/mutations.log"), log.join("\n") + "\n");
