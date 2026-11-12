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
const settled = { billId: BILL_ID, payableAmount: 110, allocatedTotal: 110, remainingAmount: 0, unsettledPayment: null,
  allocations: [{ paymentId: "p1", amount: 110, method: "Cash", paymentStatus: "Approved" }] };
const partly = { ...settled, allocatedTotal: 50, remainingAmount: 60, allocations: [{ paymentId: "p1", amount: 50, method: "Cash", paymentStatus: "Approved" }] };
const pending = {
  requestId: "r1", billId: BILL_ID, paymentId: "p1", amount: 110, method: "Cash", reason: "Yanlış tutar", status: "Requested",
  requestedBy: "u1", requestedAt: "2026-10-13T10:00:00Z", decidedBy: null, decidedAt: null, slipNumber: null, billCancelled: false, mayDecide: false,
};

async function openPaid(requests, query = "", summary = settled) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}${query}`);
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: summary },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: requests },
    { test: (url) => url.endsWith("/show-on-display"), body: {} },
  ]);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect(document.querySelector("details.sp-fold")).not.toBeNull());
}

const scroll = vi.fn();
beforeEach(() => { scroll.mockClear(); Element.prototype.scrollIntoView = scroll; });
afterEach(() => { document.body.innerHTML = ""; });

describe("the open=refund mark opens the refund section the cashier asked for", () => {
  it("opens the refund fold of a bill that still has an amount to collect", async () => {
    await openPaid([], "&open=refund", partly);

    expect(document.getElementById("fold-refund").open).toBe(true);
    expect(scroll.mock.contexts.at(-1)).toBe(document.getElementById("fold-refund"));
  });

  it("opens the refund fold of a paid bill", async () => {
    await openPaid([], "&open=refund");

    expect(document.getElementById("fold-refundPaid").open).toBe(true);
    expect(scroll.mock.contexts.at(-1)).toBe(document.getElementById("fold-refundPaid"));
  });

  it.each([["tap", (el) => el.click()], ["keyboard focus", (el) => el.dispatchEvent(new FocusEvent("focusin", { bubbles: true }))]])("stops bringing the fold into view once the cashier has used the screen (%s)", async (_name, act) => {
    await openPaid([], "&open=refund");
    act(document.getElementById("fold-refundPaid-summary"));
    scroll.mockClear();
    const select = document.querySelector('[data-reason-select="refund:p1"]');
    select.value = "Diğer";
    select.dispatchEvent(new Event("change", { bubbles: true }));

    expect(scroll).not.toHaveBeenCalled();
  });

  it("leaves both folds closed without the mark", async () => {
    await openPaid([], "", partly);

    expect(document.getElementById("fold-refund").open).toBe(false);
    expect(scroll).not.toHaveBeenCalled();
  });
});
