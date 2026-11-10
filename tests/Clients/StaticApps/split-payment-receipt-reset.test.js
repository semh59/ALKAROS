import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const settled = { billId: BILL_ID, payableAmount: 110, allocatedTotal: 110, remainingAmount: 0, allocations: [], unsettledPayment: null };

async function open(resetAnswer) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: settled },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url, init) => url.endsWith("/receipt/original-reset") && init?.method === "POST", ...resetAnswer },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.getElementById("reset-receipt")).not.toBeNull());
  return fetchMock;
}

const resetCalls = (fetchMock) => fetchMock.mock.calls
  .filter(([url, init]) => url.endsWith("/receipt/original-reset") && init?.method === "POST")
  .map(([, init]) => JSON.parse(init.body));
const type = (value) => {
  const input = document.getElementById("reset-reason");
  input.value = value;
  input.dispatchEvent(new Event("input", { bubbles: true }));
};
const text = () => document.getElementById("app").textContent;

describe("split-payment.js original receipt reset", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("refuses an empty reason without calling the server", async () => {
    const fetchMock = await open({ status: 200, body: {} });

    type("   ");
    document.getElementById("reset-receipt").click();

    expect(text()).toContain("Sıfırlama gerekçesi 1 ile 500 karakter arasında olmalı.");
    expect(resetCalls(fetchMock)).toHaveLength(0);
  });

  it("sends the trimmed reason and confirms that the next print is the original", async () => {
    const fetchMock = await open({ status: 200, body: { billId: BILL_ID, previousPrintedAt: "2026-11-10T10:00:00Z" } });

    type("  Yazıcı kağıdı bitti  ");
    document.getElementById("reset-receipt").click();
    await vi.waitFor(() => expect(text()).toContain("Orijinal fiş hakkı geri verildi; bir sonraki basım orijinal olur."));

    expect(resetCalls(fetchMock)).toEqual([{ Reason: "Yazıcı kağıdı bitti" }]);
    expect(document.getElementById("reset-reason").value).toBe("");
  });

  it("answers a refused reset in Turkish, never the code, and keeps the typed reason", async () => {
    await open({ status: 403, body: { error: { code: "FORBIDDEN", message: "Forbidden" } } });

    type("Deneme");
    document.getElementById("reset-receipt").click();
    await vi.waitFor(() => expect(text()).toContain("Bu işlem için yetkiniz yok."));

    expect(text()).not.toContain("FORBIDDEN");
    expect(document.getElementById("reset-reason").value).toBe("Deneme");
  });

  it("explains in Turkish that a bill whose receipt was never printed has no original to reset", async () => {
    await open({ status: 409, body: { error: { code: "RECEIPT_NOT_PRINTED", message: "x" } } });

    type("Deneme");
    document.getElementById("reset-receipt").click();
    await vi.waitFor(() => expect(text()).toContain("Bu hesabın fişi henüz basılmadı; sıfırlanacak orijinal yok."));
  });

  it("keeps the reason typed so far when the screen is redrawn by another action", async () => {
    await open({ status: 200, body: {} });
    document.getElementById("print-receipt").click();

    type("Yazıcı kağıdı bitti");
    document.getElementById("cancel-receipt").click();

    expect(document.getElementById("reset-reason").value).toBe("Yazıcı kağıdı bitti");
  });

  it("does not start a print while a reset is in flight", async () => {
    const fetchMock = await open({ status: 200, body: {}, delayMs: 60 });

    type("Deneme");
    document.getElementById("reset-receipt").click();
    document.getElementById("print-receipt").click();

    expect(document.getElementById("receipt-confirm")).toBeNull();
    await vi.waitFor(() => expect(document.getElementById("reset-receipt").disabled).toBe(false));
    expect(resetCalls(fetchMock)).toHaveLength(1);
  });

  it("locks the field and the button while the request is in flight", async () => {
    await open({ status: 200, body: {}, delayMs: 60 });

    type("Deneme");
    document.getElementById("reset-receipt").click();

    expect(document.getElementById("reset-reason").disabled).toBe(true);
    expect(document.getElementById("reset-receipt").disabled).toBe(true);
    await vi.waitFor(() => expect(document.getElementById("reset-receipt").disabled).toBe(false));
  });
});
