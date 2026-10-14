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

const paid = {
  billId: BILL_ID, payableAmount: 100, allocatedTotal: 100, remainingAmount: 0,
  allocations: [{ allocationId: "a1", paymentId: PAYMENT_ID, amount: 100, paymentStatus: "Approved" }],
  unsettledPayment: null,
};
const row = (status, extra = {}) => ({
  requestId: REQUEST_ID, billId: BILL_ID, paymentId: PAYMENT_ID, amount: 100, method: "Cash", reason: "İkram",
  status, requestedBy: "u1", requestedAt: "2026-10-14T10:00:00Z", decidedBy: null, decidedAt: null,
  slipNumber: null, billCancelled: false, approvedFromAllowance: false, ...extra,
});
const shift = (allowance, used) => ({ hasOpenSession: true, allowance, used, remaining: allowance - used });

async function open({ allowance = shift(0, 0), allowanceStatus = 200, requestResult = row("Requested") } = {}) {
  const base = `/api/v1/terminals/${TERMINAL_ID}/billing`;
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url) => url.endsWith("/tenders/"), body: paid },
    { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith(`${base}/refund-allowance`), status: allowanceStatus, body: allowance },
    { test: (url, init) => url.endsWith(`${base}/bills/${BILL_ID}/refund-requests`) && init?.method === "POST", body: requestResult },
    { test: (url, init) => url.endsWith(`${base}/refund-requests`) && (init?.method ?? "GET") === "GET", body: [] },
  ]);
  await loadApp(htmlPath, scriptPath);
  return fetchMock;
}

const app = () => document.getElementById("app");

async function askForRefund() {
  await vi.waitFor(() => expect(document.querySelector('[data-refund-action="request"]')).not.toBeNull());
  const select = document.querySelector(`[data-reason-select="refund:${PAYMENT_ID}"]`);
  select.value = "İkram";
  select.dispatchEvent(new Event("change", { bubbles: true }));
  document.querySelector('[data-refund-action="request"]').click();
}

describe("split-payment.js shift refund allowance", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("shows what is left of the shift allowance", async () => {
    await open({ allowance: shift(200, 120) });

    await vi.waitFor(() => expect(document.getElementById("refund-allowance")).not.toBeNull());
    expect(document.getElementById("refund-allowance").textContent).toContain("Kalan vardiya iade bütçeniz");
    expect(document.getElementById("refund-allowance").textContent).toContain("80,00");
    expect(document.getElementById("refund-allowance").textContent).not.toContain("200,00");
  });

  it("shows nothing when there is no allowance, no open session, or it cannot be read", async () => {
    await open({ allowance: shift(0, 0) });
    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Ödendi"));
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="request"]')).not.toBeNull());
    expect(document.getElementById("refund-allowance")).toBeNull();

    document.body.innerHTML = "";
    vi.resetModules();
    await open({ allowance: { hasOpenSession: false, allowance: 100, used: 0, remaining: 100 } });
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="request"]')).not.toBeNull());
    expect(document.getElementById("refund-allowance")).toBeNull();

    document.body.innerHTML = "";
    vi.resetModules();
    await open({ allowance: {}, allowanceStatus: 500 });
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="request"]')).not.toBeNull());
    expect(document.getElementById("refund-allowance")).toBeNull();
  });

  it("says a refund inside the allowance was completed from it", async () => {
    await open({ allowance: shift(150, 0), requestResult: row("Completed", { approvedFromAllowance: true }) });
    await askForRefund();

    await vi.waitFor(() => expect(app().textContent).toContain("İade vardiya bütçenizden tamamlandı."));
    expect(app().textContent).not.toContain("onay bekliyor");
  });

  it("asks for the slip number when a card refund was reserved from the allowance", async () => {
    await open({
      allowance: shift(150, 0),
      requestResult: row("AwaitingManualRefund", { method: "Manual", approvedFromAllowance: true }),
    });
    await askForRefund();

    await vi.waitFor(() => expect(app().textContent).toContain("fiş numarasını girerek tamamlayın"));
  });

  it("keeps the waiting-for-approval message when the refund went to the manager", async () => {
    await open({ allowance: shift(50, 0), requestResult: row("Requested") });
    await askForRefund();

    await vi.waitFor(() => expect(app().textContent).toContain("İade isteği gönderildi; onay bekliyor."));
  });
});
