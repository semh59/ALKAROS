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
const SESSION_ID = "66666666-6666-6666-6666-666666666666";

const unpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };
const paid = {
  ...unpaid, allocatedTotal: 100, remainingAmount: 0,
  allocations: [{ allocationId: "a1", paymentId: PAYMENT_ID, amount: 100, paymentStatus: "Approved" }],
};

function routes({ tenders = { body: unpaid }, refunds = { body: [] }, cashSession = { body: { cashSessionId: SESSION_ID } }, extra = [] }) {
  return [
    ...extra,
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", ...tenders },
    { test: (url) => url.includes("/cash-sessions/active"), ...cashSession },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url, init) => url.endsWith("/refund-requests") && (init?.method ?? "GET") === "GET", ...refunds },
  ];
}

async function open(routeSet) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter(routeSet);
  await loadApp(htmlPath, scriptPath);
  return fetchMock;
}

const app = () => document.getElementById("app");

function reasonKey(selector) {
  if (selector === "#cancel-reason") return "cancel";
  const refund = selector.match(/data-refund-reason="([^"]+)"/);
  if (refund) return "refund:" + refund[1];
  const note = selector.match(/data-refund-note="([^"]+)"/);
  return note ? "note:" + note[1] : null;
}

function type(selector, value) {
  const key = reasonKey(selector);
  if (key && !document.querySelector(selector)) {
    const select = document.querySelector('[data-reason-select="' + key + '"]');
    select.value = "Diğer";
    select.dispatchEvent(new Event("change", { bubbles: true }));
  }
  const input = document.querySelector(selector);
  input.value = value;
  input.dispatchEvent(new Event("input", { bubbles: true }));
}

describe("split-payment.js failure states", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("tells the cashier a declined payment was declined", async () => {
    await open(routes({
      cashSession: { status: 404 },
      extra: [{ test: (url, init) => url.endsWith("/tenders/") && init?.method === "POST", body: { outcome: "Declined", approvedAmount: null, reason: "x" } }],
    }));
    await vi.waitFor(() => expect(document.getElementById("eft-confirm")).not.toBeNull());
    document.getElementById("eft-confirm").click();

    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(app().textContent).toContain("Ödeme reddedildi"));
  });

  it("shows a retryable error, not the sign-in screen, when the server fails while loading the bill", async () => {
    const fetchMock = await open(routes({ tenders: { status: 500, body: {} } }));
    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Okunamadı"));
    expect(app().textContent).not.toContain("Kasa Girişi");

    const before = fetchMock.mock.calls.length;
    document.querySelector('[data-refund-action="reload"]').click();

    await vi.waitFor(() => expect(fetchMock.mock.calls.length).toBeGreaterThan(before));
  });

  it("shows the sign-in screen only when the session is gone", async () => {
    await open(routes({ tenders: { status: 401, body: {} } }));

    await vi.waitFor(() => expect(app().textContent).toContain("Kasa Girişi"));
  });

  it("shows a retryable error when the network drops while loading the bill", async () => {
    window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
    const ok = installFetchRouter(routes({}));
    ok.mockImplementation(async (url) => {
      if (String(url).endsWith("/api/v1/auth/session/current")) {
        return { ok: true, status: 200, json: async () => ({ terminalId: TERMINAL_ID, userId: "u1" }), headers: { get: () => null } };
      }
      throw new TypeError("fetch failed");
    });
    await loadApp(htmlPath, scriptPath);

    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Okunamadı"));
    expect(document.querySelector('[data-refund-action="reload"]')).not.toBeNull();
  });

  it("asks for a second confirmation before cancelling and lets the cashier back out", async () => {
    const fetchMock = await open(routes({
      extra: [{ test: (url, init) => url.endsWith("/cancel") && init?.method === "POST", body: {} }],
    }));
    await vi.waitFor(() => expect(document.querySelector('[data-reason-select="cancel"]')).not.toBeNull());
    expect(document.querySelector('[data-refund-action="cancel"]').disabled).toBe(true);

    type("#cancel-reason", "Müşteri vazgeçti");
    expect(document.querySelector('[data-refund-action="cancel"]').disabled).toBe(false);
    document.querySelector('[data-refund-action="cancel"]').click();
    expect(app().textContent).toContain("Emin misiniz");
    expect(fetchMock.mock.calls.some(([url]) => url.endsWith("/cancel"))).toBe(false);

    document.querySelector('[data-refund-action="dismiss"]').click();
    expect(app().textContent).not.toContain("Emin misiniz");
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);

    type("#cancel-reason", "  ");
    expect(document.querySelector('[data-refund-action="cancel"]').disabled).toBe(true);
  });

  it("shows the way back after a confirmed cancel", async () => {
    await open(routes({
      extra: [{ test: (url, init) => url.endsWith("/cancel") && init?.method === "POST", body: {} }],
    }));
    await vi.waitFor(() => expect(document.querySelector('[data-reason-select="cancel"]')).not.toBeNull());
    type("#cancel-reason", "Müşteri vazgeçti");

    document.querySelector('[data-refund-action="cancel"]').click();
    document.querySelector('[data-refund-action="cancel"]').click();

    await vi.waitFor(() => expect(app().textContent).toContain("Hesap İptal Edildi"));
    expect(app().querySelector('a[href="/pending-checks"]')).not.toBeNull();
  });

  it("keeps the refund buttons off until a reason is typed", async () => {
    await open(routes({ tenders: { body: paid } }));
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="request"]')).not.toBeNull());
    const button = () => document.querySelector('[data-refund-action="request"]');
    expect(button().disabled).toBe(true);

    type(`[data-refund-reason="${PAYMENT_ID}"]`, "Fazla ödeme");

    expect(button().disabled).toBe(false);
  });

  it("does not offer refunds when the request list could not be read", async () => {
    await open(routes({ tenders: { body: paid }, refunds: { status: 500, body: {} } }));
    await vi.waitFor(() => expect(app().textContent).toContain("İade istekleri okunamadı"));

    expect(document.querySelector('[data-refund-action="request"]')).toBeNull();
  });

  it("keeps the refund lines when the request list is merely forbidden for this user", async () => {
    await open(routes({ tenders: { body: paid }, refunds: { status: 403, body: {} } }));

    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="request"]')).not.toBeNull());
    expect(app().textContent).not.toContain("İade istekleri okunamadı");
  });

  it("warns that a payment may have gone through when the network drops during a tender", async () => {
    const fetchMock = await open(routes({ cashSession: { status: 404 } }));
    await vi.waitFor(() => expect(document.getElementById("eft-confirm")).not.toBeNull());
    document.getElementById("eft-confirm").click();
    const original = fetchMock.getMockImplementation();
    fetchMock.mockImplementation(async (url, init) => {
      if (init?.method === "POST") throw new TypeError("fetch failed");
      return original(url, init);
    });

    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(app().textContent).toContain("gerçekleşmiş olabilir"));
  });

  it("reloads the bill after a conflict instead of leaving the stale screen", async () => {
    const fetchMock = await open(routes({
      tenders: { body: paid },
      extra: [{ test: (url, init) => url.endsWith("/refund-requests") && init?.method === "POST", status: 409, body: {} }],
    }));
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="request"]')).not.toBeNull());
    type(`[data-refund-reason="${PAYMENT_ID}"]`, "Fazla ödeme");
    const tenderReads = () => fetchMock.mock.calls.filter(([url]) => url.endsWith("/tenders/")).length;
    const before = tenderReads();

    document.querySelector('[data-refund-action="request"]').click();

    await vi.waitFor(() => expect(tenderReads()).toBeGreaterThan(before));
  });

  it("gives a way back to the pending checks from the paid and cancelled screens", async () => {
    await open(routes({ tenders: { body: paid } }));

    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Ödendi"));

    expect(app().querySelector('a[href="/pending-checks"]')).not.toBeNull();
  });

  it("asks once before a discount that closes the whole bill and sends nothing until confirmed", async () => {
    const fetchMock = await open(routes({
      extra: [{ test: (url, init) => url.endsWith("/discount") && init?.method === "POST", body: { status: "Applied" } }],
    }));
    await vi.waitFor(() => expect(document.getElementById("submit-discount")).not.toBeNull());
    type("#discount-value", "100");
    const discountCalls = () => fetchMock.mock.calls.filter(([url]) => url.endsWith("/discount")).length;

    document.getElementById("submit-discount").click();

    await vi.waitFor(() => expect(app().textContent).toContain("Evet, indirimi uygula"));
    expect(discountCalls()).toBe(0);

    document.getElementById("submit-discount").click();

    await vi.waitFor(() => expect(discountCalls()).toBe(1));
  });

  it("applies a small discount at once without the extra question", async () => {
    const fetchMock = await open(routes({
      extra: [{ test: (url, init) => url.endsWith("/discount") && init?.method === "POST", body: { status: "Applied" } }],
    }));
    await vi.waitFor(() => expect(document.getElementById("submit-discount")).not.toBeNull());
    type("#discount-value", "10");

    document.getElementById("submit-discount").click();

    await vi.waitFor(() => expect(fetchMock.mock.calls.some(([url]) => url.endsWith("/discount"))).toBe(true));
  });
});
