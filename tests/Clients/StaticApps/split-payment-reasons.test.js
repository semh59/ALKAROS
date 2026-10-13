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
const REQUEST_ID = "44444444-4444-4444-4444-444444444444";

const unpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };
const paid = {
  ...unpaid, allocatedTotal: 100, remainingAmount: 0,
  allocations: [{ allocationId: "a1", paymentId: PAYMENT_ID, amount: 100, paymentStatus: "Approved" }],
};
const waiting = {
  requestId: REQUEST_ID, billId: BILL_ID, paymentId: PAYMENT_ID, amount: 100, method: "Cash", reason: "x", status: "Requested",
  requestedBy: "u1", requestedAt: "2026-10-13T10:00:00Z", decidedBy: null, decidedAt: null, slipNumber: null, billCancelled: false,
};

function routes({ summary, requests = [] }) {
  return [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u2" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: summary },
    { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url, init) => init?.method === "POST", body: { ...waiting, status: "Rejected" } },
    { test: (url) => url.endsWith("/refund-requests"), body: requests },
  ];
}

async function open(summary, requests) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter(routes({ summary, requests }));
  await loadApp(htmlPath, scriptPath);
  return fetchMock;
}

const select = (key) => document.querySelector(`[data-reason-select="${key}"]`);
const choose = (key, value) => {
  select(key).value = value;
  select(key).dispatchEvent(new Event("change", { bubbles: true }));
};
const post = (fetchMock, suffix) => {
  const call = fetchMock.mock.calls.find(([url, init]) => url.endsWith(suffix) && init?.method === "POST");
  return call ? JSON.parse(call[1].body) : null;
};

describe("split-payment.js preset reasons", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("cancels with a preset reason and shows no free text field", async () => {
    const fetchMock = await open(unpaid);
    await vi.waitFor(() => expect(select("cancel")).not.toBeNull());
    const button = () => document.querySelector('[data-refund-action="cancel"]');
    expect(button().disabled).toBe(true);

    choose("cancel", "Yanlış sipariş");

    expect(document.getElementById("cancel-reason")).toBeNull();
    expect(button().disabled).toBe(false);
    button().click();
    button().click();
    await vi.waitFor(() => expect(post(fetchMock, "/cancel")).toEqual({ reason: "Yanlış sipariş" }));
  });

  it("opens a free text field for Diğer and keeps the button off until it is filled", async () => {
    const fetchMock = await open(unpaid);
    await vi.waitFor(() => expect(select("cancel")).not.toBeNull());

    choose("cancel", "Diğer");
    const input = document.getElementById("cancel-reason");
    expect(input).not.toBeNull();
    expect(document.querySelector('[data-refund-action="cancel"]').disabled).toBe(true);
    input.value = "Masa değişti";
    input.dispatchEvent(new Event("input", { bubbles: true }));
    expect(document.querySelector('[data-refund-action="cancel"]').disabled).toBe(false);
    document.querySelector('[data-refund-action="cancel"]').click();
    document.querySelector('[data-refund-action="cancel"]').click();

    await vi.waitFor(() => expect(post(fetchMock, "/cancel")).toEqual({ reason: "Masa değişti" }));
  });

  it("offers the same list for a refund request and for a rejection", async () => {
    const fetchMock = await open(paid, [waiting]);
    await vi.waitFor(() => expect(select(`note:${REQUEST_ID}`)).not.toBeNull());
    expect(select(`refund:${PAYMENT_ID}`)).toBeNull();
    const options = [...select(`note:${REQUEST_ID}`).options].map((option) => option.textContent);
    expect(options).toEqual(["Gerekçe seçin", "Müşteri vazgeçti", "Yanlış sipariş", "Mutfak hatası", "Ürün sorunlu", "İkram", "Yanlış tutar girildi", "Diğer"]);

    choose(`note:${REQUEST_ID}`, "Mutfak hatası");
    document.querySelector('[data-refund-action="reject"]').click();

    await vi.waitFor(() => expect(post(fetchMock, "/reject")).toEqual({ note: "Mutfak hatası" }));
  });

  it("asks for a refund with a preset reason", async () => {
    const fetchMock = await open(paid, []);
    await vi.waitFor(() => expect(select(`refund:${PAYMENT_ID}`)).not.toBeNull());

    choose(`refund:${PAYMENT_ID}`, "Ürün sorunlu");
    document.querySelector('[data-refund-action="request"]').click();

    await vi.waitFor(() => expect(post(fetchMock, "/refund-requests")).toEqual({ paymentId: PAYMENT_ID, reason: "Ürün sorunlu" }));
  });
});
