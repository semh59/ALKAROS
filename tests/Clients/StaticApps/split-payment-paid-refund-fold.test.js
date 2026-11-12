import { afterEach, describe, expect, it, vi } from "vitest";
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
const pending = {
  requestId: "r1", billId: BILL_ID, paymentId: "p1", amount: 110, method: "Cash", reason: "Yanlış tutar", status: "Requested",
  requestedBy: "u1", requestedAt: "2026-10-13T10:00:00Z", decidedBy: null, decidedAt: null, slipNumber: null, billCancelled: false, mayDecide: false,
};

async function openPaid(requests) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: settled },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: requests },
    { test: (url) => url.endsWith("/show-on-display"), body: {} },
  ]);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());
}

afterEach(() => { document.body.innerHTML = ""; });

describe("the paid screen keeps the refund forms out of the way", () => {
  it("shows the success heading and a closed refund fold", async () => {
    await openPaid([]);

    expect(document.querySelector(".sp-title").textContent).toBe("Hesap Ödendi");
    const fold = document.getElementById("fold-refundPaid");
    expect(fold).not.toBeNull();
    expect(fold.open).toBe(false);
    expect(document.getElementById("fold-refundPaid-summary").textContent).toBe("İade");
  });

  it("opens the refund fold when a refund request is waiting", async () => {
    await openPaid([pending]);

    expect(document.getElementById("fold-refundPaid").open).toBe(true);
  });

  it("keeps the refund fold open after the cashier opened it and the screen redraws", async () => {
    await openPaid([]);
    document.getElementById("fold-refundPaid-summary").click();
    const select = document.querySelector('[data-reason-select="refund:p1"]');
    select.value = "Diğer";
    select.dispatchEvent(new Event("change", { bubbles: true }));

    expect(document.getElementById("fold-refundPaid").open).toBe(true);
  });
});
