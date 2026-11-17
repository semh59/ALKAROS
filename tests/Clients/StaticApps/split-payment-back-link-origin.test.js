import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const settled = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 100, remainingAmount: 0, allocations: [], unsettledPayment: null };
const backLinks = () => [...document.querySelectorAll("#app a.sp-btn")]
  .filter((a) => /dön$/.test(a.textContent) && /pending-checks|recent-sales|billing/.test(a.getAttribute("href")))
  .map((a) => ({ text: a.textContent, href: a.getAttribute("href") }));

async function openMissing(query) {
  window.history.replaceState({}, "", `/cashier/payments/split-payment/index.html${query}`);
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", status: 404, body: null },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.querySelector("#app .sp-title")?.textContent).toBe("Hesap Bulunamadı"));
}

async function openPaid(query) {
  window.history.replaceState({}, "", `/cashier/payments/split-payment/index.html?billId=${BILL_ID}${query}`);
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: settled },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "66666666-6666-6666-6666-666666666666" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url) => url.endsWith("/receipt"), body: { items: [], status: "Paid", tableNumber: null } },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.getElementById("print-receipt")).not.toBeNull());
}

describe("the way back from the payment page to the bill split screen", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => { vi.unstubAllGlobals(); document.body.innerHTML = ""; });

  it("adds no way back to the split screen on a paid bill: the next job is the only way on", async () => {
    await openPaid("&from=billing");

    expect(backLinks()).toEqual([]);
  });

  it("goes back to this bill's split screen when the bill could not be found there", async () => {
    await openMissing(`?billId=${BILL_ID}&from=billing`);

    expect(backLinks()).toEqual([{ text: "Hesap bölmeye dön", href: `/billing?billId=${BILL_ID}` }]);
    expect(document.querySelector("#app .sp-subtitle").textContent).toBe("Hesap açılamadı. Hesap bölme ekranından tekrar seçin.");
  });

  it("does not follow the name of another place that only starts like it", async () => {
    await openPaid("&from=billing-x");

    expect(backLinks()).toEqual([{ text: "Bekleyen hesaplara dön", href: "/pending-checks" }]);
  });
});
