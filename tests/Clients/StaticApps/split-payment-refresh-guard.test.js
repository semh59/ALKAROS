import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const htmlPath = resolve(pageDir, "index.html");
const scriptPath = resolve(pageDir, "split-payment.js");

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const SESSION_ID = "66666666-6666-6666-6666-666666666666";

const unpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };
const waitingForApproval = {
  ...unpaid,
  allocations: [{ allocationId: "a1", paymentId: "33333333-3333-3333-3333-333333333333", amount: 50, paymentStatus: "Approved" }],
  unsettledPayment: {
    paymentId: "p1", status: "RequiresReconciliation", reason: "x",
    pendingConfirmation: { confirmationId: "c1", slipNumber: "S1", amount: 100, requestedBy: "someone-else" },
  },
};

const reply = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body, headers: { get: () => null } });
const app = () => document.getElementById("app");

/**
 * Opens the page with a scripted server. `reads` answers each GET .../tenders/ in order (the last answer repeats):
 * a function returns the body (or a promise of it), "fail" answers 500, "network" throws.
 * The poll timer is captured so a test can fire a tick itself.
 */
async function open(reads) {
  let readCount = 0;
  const ticks = [];
  vi.spyOn(window, "setInterval").mockImplementation((callback) => { ticks.push(callback); return ticks.length; });
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    if (url.endsWith("/api/v1/auth/session/current")) return reply(200, { terminalId: TERMINAL_ID, userId: "u1" });
    if (url.endsWith("/tenders/") && method === "GET") {
      const next = reads[Math.min(readCount, reads.length - 1)];
      readCount += 1;
      if (next === "fail") return reply(500, {});
      if (next === "network") throw new TypeError("Failed to fetch");
      return reply(200, await next());
    }
    if (url.includes("/cash-sessions/active")) return reply(200, { cashSessionId: SESSION_ID });
    if (url.endsWith("/adjustments")) return reply(200, { adjustments: [], summary: null });
    if (url.endsWith("/refund-requests")) return reply(200, []);
    if (url.endsWith("/refund-allowance")) return reply(200, { hasOpenSession: false, allowance: 0, used: 0, remaining: 0 });
    if (url.endsWith("/receipt")) {
      return reply(200, {
        billId: BILL_ID, billNumber: "B-1", status: "Open", currencyCode: "TRY", openedAt: "2026-10-14T10:00:00Z", closedAt: null,
        items: [{ billItemId: "aaaaaaaa-0000-0000-0000-000000000001", name: "Çay", quantity: 2, unitPrice: 50, amount: 100, refundedQuantity: 0 }],
        adjustments: [], subtotal: 100, payableAmount: 100, taxTotal: 0, payments: [],
      });
    }
    if (url.endsWith("/show-on-display")) return reply(204, null);
    if (url.endsWith("/reject") && method === "POST") return reply(200, {});
    if (url.endsWith("/cash-tender") && method === "POST") return reply(200, { outcome: "Approved", approvedAmount: 100, changeAmount: 0 });
    throw new Error(`unrouted ${method} ${url}`);
  }));
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect(app().textContent).toContain("Tahsilat"));
  return { tick: () => ticks.at(-1)(), readCount: () => readCount };
}

describe("split-payment.js summary refresh guard", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("does not start another poll read while the previous one is still in flight", async () => {
    let release;
    const slow = () => new Promise((resolveRead) => { release = () => resolveRead(waitingForApproval); });
    const page = await open([async () => waitingForApproval, slow]);

    page.tick();
    page.tick();
    page.tick();

    expect(page.readCount()).toBe(2);
    release();
    await vi.waitFor(() => {
      page.tick();
      expect(page.readCount()).toBe(3);
    });
  });

  it("an older answer that arrives late never overwrites the newer one", async () => {
    let releaseOld;
    const staleRead = () => new Promise((resolveRead) => { releaseOld = () => resolveRead(waitingForApproval); });
    const page = await open([async () => waitingForApproval, staleRead, async () => unpaid]);

    page.tick();
    expect(page.readCount()).toBe(2);
    document.getElementById("reject-confirmation").click();
    await vi.waitFor(() => expect(document.getElementById("pending-confirmation")).toBeNull());

    releaseOld();
    await new Promise((resolveWait) => setTimeout(resolveWait, 20));

    expect(document.getElementById("pending-confirmation")).toBeNull();
    expect(document.getElementById("submit-tender")).not.toBeNull();
  });

  it.each(["fail", "network"])("a failed (%s) read after a tender warns that the amounts may be old, blocks a new tender, and the retry clears it", async (failure) => {
    const page = await open([async () => unpaid, failure, async () => unpaid]);
    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(document.getElementById("summary-stale-notice")).not.toBeNull());
    expect(document.getElementById("summary-stale-notice").textContent).toContain("Hesap bilgisi güncellenemedi");
    expect(document.getElementById("submit-tender").disabled).toBe(true);

    document.getElementById("retry-summary").click();

    await vi.waitFor(() => expect(document.getElementById("summary-stale-notice")).toBeNull());
    expect(document.getElementById("submit-tender").disabled).toBe(false);
    expect(page.readCount()).toBe(3);
  });

  it("a forced click on the disabled button while the amounts are stale sends nothing", async () => {
    const page = await open([async () => unpaid, "fail"]);
    document.getElementById("submit-tender").click();
    await vi.waitFor(() => expect(document.getElementById("summary-stale-notice")).not.toBeNull());
    const posts = () => globalThis.fetch.mock.calls.filter(([, init]) => init?.method === "POST").length;
    const before = posts();

    const button = document.getElementById("submit-tender");
    button.disabled = false;
    button.click();

    expect(posts()).toBe(before);
    expect(app().textContent).toContain("yeniden okuyun");
    expect(page.readCount()).toBe(2);
  });

  it("a failed poll read shows the same warning and keeps what is on screen", async () => {
    const page = await open([async () => waitingForApproval, "network"]);

    page.tick();

    await vi.waitFor(() => expect(document.getElementById("summary-stale-notice")).not.toBeNull());
    expect(document.getElementById("pending-confirmation")).not.toBeNull();
  });

  it("keeps the caret in a field that has no id when the poll redraws the page", async () => {
    const page = await open([async () => waitingForApproval]);
    const field = '[data-item-qty="aaaaaaaa-0000-0000-0000-000000000001"]';
    await vi.waitFor(() => expect(document.querySelector(field)).not.toBeNull());
    const input = document.querySelector(field);
    input.focus();
    expect(document.activeElement).toBe(input);

    page.tick();
    await vi.waitFor(() => expect(page.readCount()).toBe(2));
    await new Promise((resolveWait) => setTimeout(resolveWait, 20));

    expect(document.activeElement).toBe(document.querySelector(field));
    expect(document.activeElement).not.toBe(input);
  });
});
