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

describe("a fully paid bill with a refund request waiting is not shown as paid", () => {
  it("says why the bill stays open instead of 'Hesap Ödendi'", async () => {
    await openPaid([pending]);

    expect(document.querySelector(".sp-title").textContent).toBe("Tahsilat Tamam, Hesap Açık");
    expect(document.querySelector(".sp-icon-circle").classList.contains("success")).toBe(false);
    expect(document.querySelector(".sp-subtitle").textContent).toBe("İade isteği yönetici onayı bekliyor; karar verilene kadar hesap kapanmaz.");
  });

  it("says the approved refund still needs its slip or transfer confirmation before the bill closes", async () => {
    await openPaid([{ ...pending, status: "AwaitingManualRefund", method: "Card" }]);

    expect(document.querySelector(".sp-title").textContent).toBe("Tahsilat Tamam, Hesap Açık");
    expect(document.querySelector(".sp-icon-circle").classList.contains("success")).toBe(false);
    expect(document.querySelector(".sp-subtitle").textContent).toBe("İade onaylandı; aşağıdaki İade bölümünden kart için fiş numarasını yazıp “İadeyi tamamla”ya, havale için “Havale gönderildi”ye basın. Bu yapılana kadar hesap kapanmaz.");
  });

  it("shows the paid heading when the request was already decided", async () => {
    await openPaid([{ ...pending, status: "Rejected" }]);

    expect(document.querySelector(".sp-title").textContent).toBe("Hesap Ödendi");
    expect(document.querySelector(".sp-icon-circle").classList.contains("success")).toBe(true);
  });
});
