import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..");
const read = (path) => readFileSync(resolve(repoRoot, path), "utf8");

// Codes the payment screen can meet that carry no fixed sentence: the status fallback of the screen answers them.
const STATUS_FALLBACK = new Set(["INTERNAL_ERROR", "DATABASE_UNAVAILABLE", "REQUEST_CANCELLED", "NOT_FOUND", "FORBIDDEN", "UNAUTHORIZED", "VALIDATION_FAILED"]);
const CATALOG_CODES = ["INSUFFICIENT_CASH_TENDER", "OVER_ALLOCATION", "ACTIVE_CASH_SESSION_EXISTS", "CASH_OUT_EXCEEDS_DRAWER", "INVALID_CASH_SESSION_STATE"];
const SERVER_SOURCES = [
  "src/Host/DualScreen/DualScreenApplication.Payments.cs",
  "src/Host/Experience/Billing/BillingSplitApplication.cs",
  "src/Host/Experience/Billing/PaymentRefunds.cs",
  "src/Host/Experience/CustomerAccounts/DualScreenApplication.CustomerAccounts.cs",
];
// Exceptions that carry the code, an English log message and the sentence shown to the cashier as separate arguments.
// Merging orders is the billing page's own flow, not this screen's.
const MERGE_CODES = new Set([
  "MERGE_NEEDS_TWO_ORDERS", "MERGE_ORDER_SOURCE", "MERGE_DIFFERENT_TABLES", "MERGE_ORDER_ALREADY_BILLED", "MERGE_ORDER_EMPTY", "MERGE_MIXED_SENT", "MERGE_TOO_MANY_ORDERS",
]);
const VALIDATION_SOURCE = "src/Host/Experience/Billing/BillingSplitStore.cs";
const VALIDATION = new RegExp(String.raw`"([A-Z][A-Z0-9_]{3,})",\s*\$?"(?:[^"\\]|\\.)*",\s*(?:(\$?)"((?:[^"\\]|\\.)*)"\s*\)|)`, "g");
const REFUND_INPUT = new RegExp(String.raw`RefundInputException\(\s*\$?"(?:[^"\\]|\\.)*",\s*"([A-Z][A-Z0-9_]{3,})",\s*(\$?)"((?:[^"\\]|\\.)*)"\)`, "g");
const RESPONSE = new RegExp(
  String.raw`\(\d{3},\s*"([A-Z][A-Z0-9_]{3,})",\s*(\$?)"((?:[^"\\]|\\.)*)"`
  + String.raw`|code\s*=\s*"([A-Z][A-Z0-9_]{3,})"[^;]*?message\s*=\s*(\$?)"((?:[^"\\]|\\.)*)"`
  + String.raw`|"([A-Z][A-Z0-9_]{3,})",\s*"((?:[^"\\]|\\.)*)"`,
  "gs",
);

function serverCodes() {
  const found = new Map();
  const collect = (text, only) => {
    for (const match of text.matchAll(RESPONSE)) {
      const code = match[1] ?? match[4] ?? match[7];
      const interpolated = (match[2] ?? match[5] ?? "") === "$";
      const message = (match[3] ?? match[6] ?? match[8]).replaceAll('\\"', '"');
      if ((!only || only.includes(code)) && !found.has(code)) found.set(code, { interpolated, message });
    }
  };
  for (const source of SERVER_SOURCES) collect(read(source));
  collect(read("src/Host/Composition/Errors/ApiErrorCatalog.cs"), CATALOG_CODES);
  // A code the endpoints above already answer keeps that sentence.
  const validationSource = read(VALIDATION_SOURCE);
  const literal = new Map();
  for (const match of validationSource.matchAll(VALIDATION)) {
    literal.set(match[1], { interpolated: match[3] === undefined || match[2] === "$", message: (match[3] ?? "").replaceAll('\\"', '"') });
  }
  // Every code the exceptions are built with is checked, so a message the literal pattern cannot read counts as built per request.
  for (const [, code] of validationSource.matchAll(/new BillingValidationException\(\s*"([A-Z][A-Z0-9_]{3,})"/g)) {
    if (MERGE_CODES.has(code)) continue;
    expect(code.startsWith("MERGE_"), `${code} is a merge code that is not listed`).toBe(false);
    if (found.has(code)) continue;
    found.set(code, literal.get(code) ?? { interpolated: true, message: "" });
  }
  for (const match of read("src/Host/Experience/Billing/PaymentRefunds.cs").matchAll(REFUND_INPUT)) {
    found.set(match[1], { interpolated: match[2] === "$", message: match[3] });
  }
  const refundFallback = read("src/Host/Experience/Billing/BillingSplitApplication.cs").match(/refundInput\.UserMessage \?\? "([^"]+)"/)[1];
  found.set("REFUND_INPUT_INVALID", { interpolated: false, message: refundFallback });
  const otherTerminal = read("src/Host/Experience/Billing/BillTerminalAccess.cs").match(/UserMessage = "([^"]+)"/)[1];
  found.set("BILL_OTHER_TERMINAL", { interpolated: false, message: otherTerminal });
  return found;
}

function screenDictionary() {
  const script = read("src/Clients/Cashier/wwwroot/payments/split-payment/split-payment.js");
  const block = script.slice(script.indexOf("var ERROR_TEXTS = {"), script.indexOf("var SERVER_TEXT_CODES"));
  const entries = new Map();
  for (const match of block.matchAll(/^\s+([A-Z][A-Z0-9_]+): '((?:[^'\\]|\\.)*)',$/gm)) entries.set(match[1], match[2].replaceAll("\\'", "'"));
  const serverText = script.match(/var SERVER_TEXT_CODES = \[([^\]]*)\]/)[1].match(/[A-Z_]+/g);
  return { entries, serverText };
}

describe("payment screen error dictionary", () => {
  const server = serverCodes();
  const { entries, serverText } = screenDictionary();

  it("finds the server codes and the screen sentences it compares", () => {
    expect(server.size).toBeGreaterThanOrEqual(50);
    expect(entries.size).toBeGreaterThanOrEqual(45);
    for (const code of ["DISCOUNT_TYPE_INVALID", "IDEMPOTENCY_KEY_REQUIRED", "TIP_AMOUNT_INVALID", "ALLOWANCE_INVALID", "REFUND_TEXT_INVALID", "REFUND_INPUT_INVALID", "BILL_OTHER_TERMINAL", "DISCOUNT_REASON_INVALID"]) {
      expect(server.has(code), `${code} is read from the server sources`).toBe(true);
    }
  });

  it("has one sentence for every fixed-text code the tender, refund, split and account endpoints can send, worded exactly like the server", () => {
    const missing = [];
    const different = [];
    for (const [code, { interpolated, message }] of server) {
      if (STATUS_FALLBACK.has(code) || interpolated) continue;
      if (!entries.has(code)) missing.push(code);
      else if (entries.get(code) !== message) different.push(code);
    }
    expect(missing).toEqual([]);
    expect(different).toEqual([]);
  });

  it("lets the server text through only for the codes whose sentence carries this request's amounts or credit reason", () => {
    const interpolated = [...server].filter(([, value]) => value.interpolated).map(([code]) => code);
    for (const code of interpolated) expect([...serverText, ...STATUS_FALLBACK]).toContain(code);
    expect([...serverText].sort()).toEqual([
      "ALLOWANCE_INVALID", "CREDIT_DENIED", "DISCOUNT_REASON_INVALID", "DISCOUNT_VALUE_INVALID", "NOTES_TOO_LONG", "RECEIPT_EXCEEDS_BALANCE",
      "REFUND_TEXT_INVALID", "TENDER_OVER_ALLOCATION", "TENDER_UNSETTLED_PAYMENT_EXISTS", "TIP_AMOUNT_INVALID",
    ]);
    for (const code of serverText) expect(entries.has(code)).toBe(false);
  });
});
