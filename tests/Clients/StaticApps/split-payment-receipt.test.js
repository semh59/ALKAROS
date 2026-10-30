import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const htmlPath = resolve(pageDir, "index.html");
const scriptPath = resolve(pageDir, "split-payment.js");

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";

const settled = { billId: BILL_ID, payableAmount: 110, allocatedTotal: 110, remainingAmount: 0, allocations: [], unsettledPayment: null };

const receipt = {
  billId: BILL_ID,
  billNumber: "H-1042",
  status: "Paid",
  currencyCode: "TRY",
  openedAt: "2026-10-13T10:05:00Z",
  closedAt: "2026-10-13T10:40:00Z",
  items: [
    { name: "Taş fırın pizza", quantity: 2, unitPrice: 40, amount: 80 },
    { name: "Ayran <b>", quantity: 1, unitPrice: 20, amount: 20 },
  ],
  adjustments: [
    { type: "Tip", amount: 20, isDeduction: false },
    { type: "DiscountAmount", amount: 10, isDeduction: true },
  ],
  subtotal: 100,
  payableAmount: 110,
  taxTotal: 0,
  payments: [
    { paymentId: "p1", amount: 60, method: "Cash", status: "Approved", at: "2026-10-13T10:30:00Z" },
    { paymentId: "p2", amount: 30, method: "Account", status: "Approved", at: "2026-10-13T10:31:00Z" },
    { paymentId: "p3", amount: 20, method: "Manual", status: "Refunded", at: "2026-10-13T10:32:00Z" },
    { paymentId: "p4", amount: 15, method: "Card", status: "Approved", at: "2026-10-13T10:33:00Z" },
  ],
};

async function open(receiptRoute) {
  const routeSet = [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url) => url.endsWith("/tenders/"), body: settled },
    { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url) => url.endsWith("/receipt"), ...receiptRoute },
  ];
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter(routeSet);
  await loadApp(htmlPath, scriptPath);
  return fetchMock;
}

const app = () => document.getElementById("app");
const sheet = () => document.getElementById("receipt-print");

describe("split-payment.js information receipt", () => {
  let printSpy;
  beforeEach(() => {
    vi.resetModules();
    printSpy = vi.fn();
    window.print = printSpy;
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("reads the receipt from the server, fills the print sheet and opens the print dialog", async () => {
    const fetchMock = await open({ body: receipt });
    await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());

    document.getElementById("print-receipt").click();

    await vi.waitFor(() => expect(printSpy).toHaveBeenCalledTimes(1));
    expect(fetchMock.mock.calls.some(([url]) => url.endsWith(`/billing/bills/${BILL_ID}/receipt`))).toBe(true);
    const text = sheet().textContent;
    expect(text).toContain("Bilgi Fişi");
    expect(text).toContain("Bu belge mali fiş değildir.");
    expect(text).toContain("H-1042");
    expect(text).toContain("2 × Taş fırın pizza");
    expect(text).toContain("80,00");
    expect(text).toContain("Toplam");
    expect(text).toContain("110,00");
  });

  it("labels adjustments, deductions, payment methods and refunded payments in Turkish and escapes item names", async () => {
    await open({ body: receipt });
    await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());

    document.getElementById("print-receipt").click();
    await vi.waitFor(() => expect(sheet()).not.toBeNull());

    const text = sheet().textContent;
    expect(text).toContain("Bahşiş");
    expect(text).toContain("İndirim");
    expect(text).toMatch(/İndirim-₺10,00|İndirim-\s?₺?\s?10,00/);
    expect(text).toContain("Nakit");
    expect(text).toContain("Cari hesap");
    expect(text).toContain("Banka kartı");
    expect(text).toContain("EFT / diğer (İade edildi)");
    expect(sheet().querySelector("b")).toBeNull();
    expect(text).toContain("Ayran <b>");
    expect(text).not.toMatch(/Approved|Refunded|Manual|Card|DiscountAmount/);
  });

  it("prints the tax per rate with a total and the line discount under its item", async () => {
    const taxed = {
      ...receipt,
      items: [{ name: "Menü", quantity: 1, unitPrice: 120, amount: 100, lineDiscount: 20 }],
      adjustments: [],
      subtotal: 100,
      payableAmount: 100,
      taxTotal: 14,
      taxLines: [
        { ratePercent: 10, net: 50, tax: 5, gross: 55 },
        { ratePercent: 20, net: 37.5, tax: 9, gross: 45 },
      ],
    };
    await open({ body: taxed });
    await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());

    document.getElementById("print-receipt").click();

    await vi.waitFor(() => expect(printSpy).toHaveBeenCalledTimes(1));
    const text = document.getElementById("receipt-print").textContent;
    expect(text).toContain("İndirim / ikram");
    expect(text).toMatch(/KDV %10 \(matrah [^)]*50,00\)/);
    expect(text).toMatch(/KDV %20 \(matrah [^)]*37,50\)/);
    expect(text).toContain("Toplam KDV");
    expect(text).toContain("14,00");
  });

  it("lists refunded units, the amount paid back per payment and the net amount kept", async () => {
    const refunded = {
      ...receipt,
      items: [
        { name: "Çay", quantity: 3, unitPrice: 50, amount: 150, billItemId: "i1", refundedQuantity: 3, completedRefundedQuantity: 2 },
        { name: "Tost", quantity: 1, unitPrice: 50, amount: 50, billItemId: "i2", refundedQuantity: 0, completedRefundedQuantity: 0 },
      ],
      payments: [
        { paymentId: "p1", amount: 200, method: "Cash", status: "PartiallyRefunded", at: "2026-10-14T10:30:00Z", refundedAmount: 100 },
      ],
    };
    await open({ body: refunded });
    await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());

    document.getElementById("print-receipt").click();

    await vi.waitFor(() => expect(printSpy).toHaveBeenCalledTimes(1));
    const sheet = document.getElementById("receipt-print").textContent;
    expect(sheet).toContain("İade edildi: 2 adet");
    expect(sheet.match(/İade edildi: /g)).toHaveLength(1);
    expect(sheet).toContain("Kısmen iade edildi");
    expect(sheet).toContain("-₺100,00");
    expect(sheet).toMatch(/Net tahsilats*₺100,00/);
  });

  it("prints no refunded units for a refund that is still waiting for approval", async () => {
    const pending = {
      ...receipt,
      items: [{ name: "Çay", quantity: 3, unitPrice: 50, amount: 150, billItemId: "i1", refundedQuantity: 2, completedRefundedQuantity: 0 }],
    };
    await open({ body: pending });
    await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());

    document.getElementById("print-receipt").click();

    await vi.waitFor(() => expect(printSpy).toHaveBeenCalledTimes(1));
    expect(document.getElementById("receipt-print").textContent).not.toContain("İade edildi:");
  });

  it("shows no refund rows and no net line when nothing was paid back", async () => {
    await open({ body: { ...receipt, items: receipt.items.map(i => ({ ...i, refundedQuantity: 0, completedRefundedQuantity: 0 })), payments: receipt.payments.map((payment) => ({ ...payment, refundedAmount: 0 })) } });
    await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());

    document.getElementById("print-receipt").click();

    await vi.waitFor(() => expect(printSpy).toHaveBeenCalledTimes(1));
    const sheet = document.getElementById("receipt-print").textContent;
    expect(sheet).not.toContain("Net tahsilat");
    expect(sheet).not.toContain("İade edildi:");
  });

  it("shows a fixed Turkish message and prints nothing when the receipt cannot be read", async () => {
    await open({ status: 500, body: {} });
    await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());

    document.getElementById("print-receipt").click();

    await vi.waitFor(() => expect(app().textContent).toContain("Fiş verisi okunamadı. Tekrar deneyin."));
    expect(printSpy).not.toHaveBeenCalled();
    expect(document.getElementById("print-receipt")).not.toBeNull();
  });

  it("prints nothing when the server refuses, even if the refusal carries a body", async () => {
    await open({ status: 500, body: receipt });
    await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());

    document.getElementById("print-receipt").click();

    await vi.waitFor(() => expect(app().textContent).toContain("Fiş verisi okunamadı"));
    expect(printSpy).not.toHaveBeenCalled();
    expect(sheet()).toBeNull();
  });

  it("disables the button while the receipt is being read so it is read once", async () => {
    const fetchMock = await open({ body: receipt, delayMs: 50 });
    await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());
    const receiptReads = () => fetchMock.mock.calls.filter(([url]) => url.endsWith("/receipt")).length;
    await vi.waitFor(() => expect(receiptReads()).toBe(1));
    await new Promise((done) => setTimeout(done, 100));

    document.getElementById("print-receipt").click();
    expect(document.getElementById("print-receipt").disabled).toBe(true);
    document.getElementById("print-receipt").click();

    await vi.waitFor(() => expect(printSpy).toHaveBeenCalledTimes(1));
    expect(receiptReads()).toBe(2);
  });

  it("keeps the sheet out of the screen layout and only the sheet in the printed page", async () => {
    const css = (await import("node:fs")).readFileSync(resolve(pageDir, "split-payment.css"), "utf8");

    expect(css).toMatch(/#receipt-print\s*\{\s*display:\s*none;?\s*\}/);
    expect(css).toMatch(/@media print[\s\S]*body > \*:not\(#receipt-print\)\s*\{\s*display:\s*none !important/);
  });
});
