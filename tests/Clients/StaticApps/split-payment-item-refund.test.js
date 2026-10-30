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
const PAYMENT_ID = "33333333-3333-3333-3333-333333333333";
const TEA = "aaaaaaaa-0000-0000-0000-000000000001";
const TOAST = "aaaaaaaa-0000-0000-0000-000000000002";

const summary = (status = "Approved") => ({
  billId: BILL_ID, payableAmount: 200, allocatedTotal: 200, remainingAmount: 0,
  allocations: [{ allocationId: "a1", paymentId: PAYMENT_ID, amount: 200, paymentStatus: status }],
  unsettledPayment: null,
});
const line = (id, name, quantity, refundedQuantity = 0) => ({
  billItemId: id, name, quantity, unitPrice: 50, amount: 50 * quantity, refundedQuantity,
});
const receiptBody = (items) => ({
  billId: BILL_ID, billNumber: "B-1", status: "Open", currencyCode: "TRY", openedAt: "2026-10-14T10:00:00Z", closedAt: null,
  items, adjustments: [], subtotal: 200, payableAmount: 200, taxTotal: 0, payments: [],
});
const row = (extra = {}) => ({
  requestId: "44444444-4444-4444-4444-444444444444", billId: BILL_ID, paymentId: PAYMENT_ID, amount: 90, method: "Cash",
  reason: "İkram", status: "Requested", requestedBy: "u1", requestedAt: "2026-10-14T10:00:00Z", decidedBy: null,
  decidedAt: null, slipNumber: null, billCancelled: false, approvedFromAllowance: false, ...extra,
});

async function open({
  status = "Approved", items = [line(TEA, "Çay", 3, 1), line(TOAST, "Tost", 1)], receiptStatus = 200,
  requests = [], requestResult = row(), requestsStatus = 200,
} = {}) {
  const base = `/api/v1/terminals/${TERMINAL_ID}/billing`;
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url) => url.endsWith("/tenders/"), body: summary(status) },
    { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith(`${base}/refund-allowance`), body: { hasOpenSession: false, allowance: 0, used: 0, remaining: 0 } },
    { test: (url) => url.endsWith(`/bills/${BILL_ID}/receipt`), status: receiptStatus, body: receiptBody(items) },
    { test: (url, init) => url.endsWith(`${base}/bills/${BILL_ID}/refund-requests`) && init?.method === "POST", body: requestResult },
    { test: (url, init) => url.endsWith(`${base}/refund-requests`) && (init?.method ?? "GET") === "GET", status: requestsStatus, body: requests },
  ]);
  await loadApp(htmlPath, scriptPath);
  return fetchMock;
}

const app = () => document.getElementById("app");
const quantity = (id) => document.querySelector(`[data-item-qty="${id}"]`);
const submit = () => document.querySelector('[data-refund-action="request-items"]');

function typeQuantity(id, value) {
  const input = quantity(id);
  input.value = value;
  input.dispatchEvent(new Event("input", { bubbles: true }));
}

function choose(selector, value) {
  const select = document.querySelector(selector);
  select.value = value;
  select.dispatchEvent(new Event("change", { bubbles: true }));
}

function chooseReason(value) {
  choose('[data-reason-select="items"]', value);
}

function posted(fetchMock) {
  const call = fetchMock.mock.calls.find(([url, init]) => url.endsWith("/refund-requests") && init?.method === "POST");
  return call ? JSON.parse(call[1].body) : null;
}

async function fillAll({ stock = "yes" } = {}) {
  await vi.waitFor(() => expect(quantity(TEA)).not.toBeNull());
  typeQuantity(TEA, "2");
  choose("[data-item-payment]", PAYMENT_ID);
  choose("[data-item-stock]", stock);
  chooseReason("İkram");
}

describe("split-payment.js item based refund", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("lists items with what is left and sends the chosen units, payment, stock choice and reason", async () => {
    const fetchMock = await open();
    await fillAll();
    expect(app().textContent).toContain("Çay · kalan 2");
    expect(app().textContent).toContain("Tost · kalan 1");

    expect(submit().disabled).toBe(false);
    submit().click();

    await vi.waitFor(() => expect(posted(fetchMock)).not.toBeNull());
    expect(posted(fetchMock)).toEqual({
      paymentId: PAYMENT_ID, reason: "İkram", items: [{ billItemId: TEA, quantity: 2, restoreStock: true }],
    });
    await vi.waitFor(() => expect(app().textContent).toContain("İade isteği gönderildi; onay bekliyor."));
    expect(app().textContent).toContain("90,00");
  });

  it("keeps the button off until quantity, payment, stock choice and reason are all given", async () => {
    await open();
    await vi.waitFor(() => expect(quantity(TEA)).not.toBeNull());
    expect(submit().disabled).toBe(true);

    typeQuantity(TEA, "1");
    expect(submit().disabled).toBe(true);
    choose("[data-item-payment]", PAYMENT_ID);
    expect(submit().disabled).toBe(true);
    choose("[data-item-stock]", "no");
    expect(submit().disabled).toBe(true);
    chooseReason("İkram");
    expect(submit().disabled).toBe(false);
  });

  it("refuses a quantity above what is left, a non-number and a zero-only selection", async () => {
    await open();
    await fillAll();
    expect(submit().disabled).toBe(false);

    typeQuantity(TEA, "3");
    expect(submit().disabled).toBe(true);
    typeQuantity(TEA, "abc");
    expect(submit().disabled).toBe(true);
    typeQuantity(TEA, "0");
    expect(submit().disabled).toBe(true);
    typeQuantity(TEA, "1,5");
    expect(submit().disabled).toBe(false);
  });

  it("needs every piece: leaving out any one keeps the button off", async () => {
    await open();
    await vi.waitFor(() => expect(quantity(TEA)).not.toBeNull());
    const steps = {
      quantity: () => typeQuantity(TEA, "1"),
      payment: () => choose("[data-item-payment]", PAYMENT_ID),
      stock: () => choose("[data-item-stock]", "no"),
      reason: () => chooseReason("İkram"),
    };

    for (const skipped of Object.keys(steps)) {
      document.body.innerHTML = "";
      vi.resetModules();
      await open();
      await vi.waitFor(() => expect(quantity(TEA)).not.toBeNull());
      for (const [name, step] of Object.entries(steps)) if (name !== skipped) step();
      expect(submit().disabled, "without " + skipped).toBe(true);
    }
  });

  it("offers no item refund when the refund list cannot be read", async () => {
    await open({ requestsStatus: 500 });
    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Ödendi"));
    await new Promise((done) => setTimeout(done, 50));

    expect(document.getElementById("item-refund")).toBeNull();
  });

  it("accepts a typed reason through the other choice", async () => {
    const fetchMock = await open();
    await vi.waitFor(() => expect(quantity(TEA)).not.toBeNull());
    typeQuantity(TEA, "1");
    choose("[data-item-payment]", PAYMENT_ID);
    choose("[data-item-stock]", "no");
    chooseReason("Diğer");
    const text = document.querySelector("[data-item-reason]");
    text.value = "Masa değişti";
    text.dispatchEvent(new Event("input", { bubbles: true }));

    submit().click();

    await vi.waitFor(() => expect(posted(fetchMock)).not.toBeNull());
    expect(posted(fetchMock)).toEqual({
      paymentId: PAYMENT_ID, reason: "Masa değişti", items: [{ billItemId: TEA, quantity: 1, restoreStock: false }],
    });
  });

  it("offers a partly refunded payment and hides the block when nothing is left or the items cannot be read", async () => {
    await open({ status: "PartiallyRefunded" });
    await vi.waitFor(() => expect(quantity(TEA)).not.toBeNull());

    document.body.innerHTML = "";
    vi.resetModules();
    await open({ items: [line(TEA, "Çay", 3, 3)] });
    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Ödendi"));
    await new Promise((done) => setTimeout(done, 50));
    expect(document.getElementById("item-refund")).toBeNull();

    document.body.innerHTML = "";
    vi.resetModules();
    await open({ receiptStatus: 500 });
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="request"]')).not.toBeNull());
    await new Promise((done) => setTimeout(done, 50));
    expect(document.getElementById("item-refund")).toBeNull();
  });

  it("offers no item refund while a request for the payment is still open", async () => {
    await open({ requests: [row()] });
    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Ödendi"));
    await new Promise((done) => setTimeout(done, 50));

    expect(document.getElementById("item-refund")).toBeNull();
  });

  it("says a refund inside the allowance was completed from it and clears the form", async () => {
    await open({ requestResult: row({ status: "Completed", approvedFromAllowance: true }) });
    await fillAll();

    submit().click();

    await vi.waitFor(() => expect(app().textContent).toContain("İade vardiya bütçenizden tamamlandı."));
    expect(quantity(TEA).value).toBe("");
  });
});
