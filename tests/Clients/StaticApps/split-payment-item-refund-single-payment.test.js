import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const PAY_1 = "33333333-3333-3333-3333-333333333331";
const PAY_2 = "33333333-3333-3333-3333-333333333332";
const TEA = "aaaaaaaa-0000-0000-0000-000000000001";

const allocation = (id, paymentId) => ({ allocationId: id, paymentId, amount: 100, method: "Cash", paymentStatus: "Approved" });
const summary = (payments) => ({
  billId: BILL_ID, payableAmount: 100 * payments.length, allocatedTotal: 100 * payments.length, remainingAmount: 0,
  allocations: payments.map((paymentId, index) => allocation(`a${index}`, paymentId)), unsettledPayment: null,
});
const receiptBody = {
  billId: BILL_ID, billNumber: "B-1", status: "Open", currencyCode: "TRY", openedAt: "2026-10-14T10:00:00Z", closedAt: null,
  items: [{ billItemId: TEA, name: "Çay", quantity: 3, unitPrice: 50, amount: 150, refundedQuantity: 0 }],
  adjustments: [], subtotal: 150, payableAmount: 150, taxTotal: 0, payments: [],
};

async function open(payments) {
  const base = `/api/v1/terminals/${TERMINAL_ID}/billing`;
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url) => url.endsWith("/tenders/"), body: summary(payments) },
    { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith(`${base}/refund-allowance`), body: { hasOpenSession: false, allowance: 0, used: 0, remaining: 0 } },
    { test: (url) => url.endsWith(`/bills/${BILL_ID}/receipt`), body: receiptBody },
    { test: (url, init) => url.endsWith(`${base}/bills/${BILL_ID}/refund-requests`) && init?.method === "POST", body: { requestId: "r1", billId: BILL_ID, paymentId: PAY_1, amount: 50, method: "Cash", reason: "İkram", status: "Requested", requestedBy: "u1", requestedAt: "2026-10-14T10:00:00Z", decidedBy: null, decidedAt: null, slipNumber: null, billCancelled: false, approvedFromAllowance: false } },
    { test: (url, init) => url.endsWith(`${base}/refund-requests`) && (init?.method ?? "GET") === "GET", body: [] },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.querySelector("[data-item-payment]")).not.toBeNull());
  return fetchMock;
}

const paymentSelect = () => document.querySelector("[data-item-payment]");
const stockSelect = () => document.querySelector("[data-item-stock]");
const submit = () => document.querySelector('[data-refund-action="request-items"]');
const choose = (select, value) => { select.value = value; select.dispatchEvent(new Event("change", { bubbles: true })); };
function fillRest() {
  const qty = document.querySelector(`[data-item-qty="${TEA}"]`);
  qty.value = "1";
  qty.dispatchEvent(new Event("input", { bubbles: true }));
  choose(document.querySelector('[data-reason-select="items"]'), "İkram");
  choose(stockSelect(), "no");
}

describe("item refund form with a single payment", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => { vi.unstubAllGlobals(); document.body.innerHTML = ""; });

  it("selects the only payment and leaves the stock choice empty", async () => {
    await open([PAY_1]);

    expect(paymentSelect().value).toBe(PAY_1);
    expect(stockSelect().value).toBe("");
  });

  it("asks for the payment when there are two", async () => {
    await open([PAY_1, PAY_2]);

    expect(paymentSelect().value).toBe("");
    fillRest();
    expect(submit().disabled).toBe(true);
    expect(submit().nextElementSibling.textContent).toBe("İadenin düşeceği ödemeyi seçin.");
    choose(paymentSelect(), PAY_2);
    expect(submit().disabled).toBe(false);
  });

  it("can be submitted without touching the payment and sends that payment", async () => {
    const fetchMock = await open([PAY_1]);
    fillRest();

    expect(submit().disabled).toBe(false);
    submit().click();
    submit().click();
    await vi.waitFor(() => expect(fetchMock.mock.calls.some(([url, init]) => url.endsWith("/refund-requests") && init?.method === "POST")).toBe(true));
    const call = fetchMock.mock.calls.find(([url, init]) => url.endsWith("/refund-requests") && init?.method === "POST");
    expect(JSON.parse(call[1].body).paymentId).toBe(PAY_1);
  });

  it("keeps the submit off until the stock is chosen", async () => {
    await open([PAY_1]);
    const qty = document.querySelector(`[data-item-qty="${TEA}"]`);
    qty.value = "1";
    qty.dispatchEvent(new Event("input", { bubbles: true }));
    choose(document.querySelector('[data-reason-select="items"]'), "İkram");

    expect(submit().disabled).toBe(true);
  });
});
