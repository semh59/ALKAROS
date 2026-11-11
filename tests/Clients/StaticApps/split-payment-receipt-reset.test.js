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

async function open(resetAnswer, capabilities = ["payments.refund.approve"], summary = settled) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1", capabilities } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: summary },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url, init) => url.endsWith("/receipt/original-reset") && init?.method === "POST", ...resetAnswer },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());
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

  it.each([[[]], [["payments.take", "cash.drawer"]], [null], [undefined], ["payments.refund.approve.x"]])(
    "does not draw the manager only reset card for a session without the refund approval (%j)",
    async (capabilities) => {
      const fetchMock = await open({ status: 200, body: {} }, capabilities === undefined ? null : capabilities);

      expect(document.getElementById("print-receipt")).not.toBeNull();
      expect(document.getElementById("reset-receipt")).toBeNull();
      expect(document.getElementById("reset-reason")).toBeNull();
      expect(text()).not.toContain("Orijinal fişi sıfırla");
      expect(resetCalls(fetchMock)).toHaveLength(0);
    },
  );

  it("draws the reset card for a session that holds the refund approval, and only on the settled page", async () => {
    await open({ status: 200, body: {} });
    expect(text()).toContain("Orijinal fişi sıfırla (yönetici)");

    document.body.innerHTML = "";
    vi.resetModules();
    window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
    installFetchRouter([
      { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1", capabilities: ["payments.refund.approve"] } },
      { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: { ...settled, allocatedTotal: 0, remainingAmount: 110 } },
      { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } },
      { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
      { test: (url) => url.endsWith("/refund-requests"), body: [] },
    ]);
    await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
    await vi.waitFor(() => expect(document.getElementById("submit-tender")).not.toBeNull());

    expect(document.getElementById("reset-receipt")).toBeNull();
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

    expect(resetCalls(fetchMock)).toEqual([{ Reason: "Yazıcı kağıdı bitti", IdempotencyKey: expect.stringMatching(/^[0-9a-f-]{36}$/) }]);
    expect(document.getElementById("reset-reason").value).toBe("");
  });

  it("resends the same key after a lost answer and uses a new key once the server has answered", async () => {
    const fetchMock = await open({ status: 200, body: { billId: BILL_ID, previousPrintedAt: "2026-11-10T10:00:00Z" } });
    const routed = fetchMock.getMockImplementation();
    fetchMock.mockImplementationOnce(async (url, init) => {
      if (url.endsWith("/receipt/original-reset")) throw new TypeError("network");
      return routed(url, init);
    });

    type("Yazıcı kağıdı bitti");
    document.getElementById("reset-receipt").click();
    await vi.waitFor(() => expect(text()).toContain("Bağlantı kurulamadı."));
    document.getElementById("reset-receipt").click();
    await vi.waitFor(() => expect(text()).toContain("Orijinal fiş hakkı geri verildi"));
    type("Yine bitti");
    document.getElementById("reset-receipt").click();
    await vi.waitFor(() => expect(resetCalls(fetchMock)).toHaveLength(3));

    const [lost, retry, next] = resetCalls(fetchMock).map((body) => body.IdempotencyKey);
    expect(retry).toBe(lost);
    expect(next).not.toBe(retry);
  });

  it("keeps the key after a server error and takes a new one when the reason changes", async () => {
    const fetchMock = await open({ status: 503, body: { error: { code: "UNAVAILABLE", message: "x" } } });

    type("Yazıcı kağıdı bitti");
    document.getElementById("reset-receipt").click();
    await vi.waitFor(() => expect(resetCalls(fetchMock)).toHaveLength(1));
    await vi.waitFor(() => expect(document.getElementById("reset-receipt").disabled).toBe(false));
    document.getElementById("reset-receipt").click();
    await vi.waitFor(() => expect(resetCalls(fetchMock)).toHaveLength(2));
    await vi.waitFor(() => expect(document.getElementById("reset-receipt").disabled).toBe(false));
    type("Başka gerekçe");
    document.getElementById("reset-receipt").click();
    await vi.waitFor(() => expect(resetCalls(fetchMock)).toHaveLength(3));

    const [first, retry, changed] = resetCalls(fetchMock).map((body) => body.IdempotencyKey);
    expect(retry).toBe(first);
    expect(changed).not.toBe(retry);
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

  it.each([
    ["RECEIPT_RESET_SELF", 403, "Orijinal fişi siz bastınız; hakkı başka bir yönetici sıfırlamalı."],
    ["RECEIPT_RESET_LIMIT", 409, "Bu hesabın orijinal fiş hakkı en fazla 2 kez sıfırlanabilir."],
  ])("answers %s in Turkish, never the code", async (code, status, message) => {
    await open({ status, body: { error: { code, message: "x" } } });

    type("Deneme");
    document.getElementById("reset-receipt").click();
    await vi.waitFor(() => expect(text()).toContain(message));
    expect(text()).not.toContain(code);
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
