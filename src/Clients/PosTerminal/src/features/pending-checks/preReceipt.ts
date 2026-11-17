import { notifySessionExpired } from "../../api";
import { formatMoney, formatQuantity, formatShortDateTime } from "../../format";
import { preReceiptText } from "../../strings";
import "./pre-receipt.css";

interface PreReceiptSheet {
  orderId: string;
  printNumber: number;
  printedAt: string;
  tableNumber: string | null;
  currencyCode: string;
  lines: readonly { name: string; quantity: number; unitPrice: number; amount: number }[];
  adjustments: readonly { type: string; amount: number; isDeduction: boolean }[];
  total: number;
  paidAmount: number;
}

export class PreReceiptError extends Error {}

const SHEET_ID = "pre-receipt-print";

function row(label: string, value: string, className?: string): HTMLTableRowElement {
  const tr = document.createElement("tr");
  if (className) tr.className = className;
  for (const text of [label, value]) tr.appendChild(document.createElement("td")).textContent = text;
  return tr;
}

function drawSheet(sheet: PreReceiptSheet): void {
  const money = (value: number) => formatMoney(value, sheet.currencyCode);
  const root = document.getElementById(SHEET_ID) ?? Object.assign(document.createElement("div"), { id: SHEET_ID });
  root.replaceChildren();
  const heading = document.createElement("h1");
  heading.textContent = preReceiptText.title;
  const notice = document.createElement("p");
  notice.className = "pre-receipt__note";
  notice.textContent = preReceiptText.notFiscal;
  const info = document.createElement("p");
  info.append(
    preReceiptText.number(sheet.printNumber),
    ...(sheet.tableNumber ? [document.createElement("br"), preReceiptText.table(sheet.tableNumber)] : []),
    document.createElement("br"),
    formatShortDateTime(sheet.printedAt),
  );
  const lines = document.createElement("table");
  for (const line of sheet.lines) lines.appendChild(row(`${formatQuantity(line.quantity)} × ${line.name}`, money(line.amount)));
  const totals = document.createElement("table");
  for (const adjustment of sheet.adjustments) {
    totals.appendChild(row(preReceiptText.adjustments[adjustment.type] ?? preReceiptText.adjustmentFallback, `${adjustment.isDeduction ? "-" : ""}${money(adjustment.amount)}`));
  }
  totals.appendChild(row(preReceiptText.total, money(sheet.total), "pre-receipt__total"));
  if (sheet.paidAmount > 0) {
    totals.appendChild(row(preReceiptText.paid, money(sheet.paidAmount)));
    totals.appendChild(row(preReceiptText.remaining, money(Math.max(0, sheet.total - sheet.paidAmount)), "pre-receipt__total"));
  }
  root.append(heading, notice, info, lines, totals);
  if (!root.isConnected) document.body.appendChild(root);
}

/**
 * The key of a print the server may already have recorded: a lost answer or a timeout leaves it in place so the retry gets
 * the same number back, and any answer that settles the print (printed, or refused for good) drops it.
 */
const unsettledKeys = new Map<string, string>();

function endPrint(): void {
  document.body.classList.remove("pre-receipt-printing");
  document.getElementById(SHEET_ID)?.remove();
}

/**
 * Prints the pre-payment slip of an order: the server numbers the print (its own counter, apart from the paid bill's
 * original and copy) and hands back the lines to draw; nothing is drawn or printed unless the server recorded the print.
 * Never opens a bill, so the table can still take items. The slip exists in the page only while it prints, so no other
 * print of the till (the table QR labels) can pick it up.
 */
export async function printPreReceipt(terminalId: string, orderId: string, fetcher: typeof fetch = fetch, print: () => void = () => window.print()): Promise<void> {
  const key = unsettledKeys.get(orderId) ?? crypto.randomUUID();
  unsettledKeys.set(orderId, key);
  let response: Response;
  try {
    response = await fetcher(`/api/v1/terminals/${encodeURIComponent(terminalId)}/orders/${encodeURIComponent(orderId)}/pre-receipt`, {
      method: "POST",
      credentials: "same-origin",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ idempotencyKey: key }),
      signal: AbortSignal.timeout(8_000),
    });
  } catch {
    throw new PreReceiptError(preReceiptText.offline);
  }
  if (!response.ok) {
    if (response.status < 500) unsettledKeys.delete(orderId);
    if (response.status === 401) notifySessionExpired();
    const payload = await response.json().catch(() => undefined) as { error?: { code?: string } } | undefined;
    throw new PreReceiptError(preReceiptText.failures[payload?.error?.code ?? ""] ?? preReceiptText.failed);
  }
  let sheet: PreReceiptSheet;
  try {
    sheet = await response.json() as PreReceiptSheet;
  } catch {
    throw new PreReceiptError(preReceiptText.failed);
  }
  unsettledKeys.delete(orderId);
  drawSheet(sheet);
  document.body.classList.add("pre-receipt-printing");
  window.addEventListener("afterprint", endPrint, { once: true });
  print();
}
