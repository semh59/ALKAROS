import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const unpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };

describe("the closed-drawer notice brings the cashier back to this bill", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => { vi.unstubAllGlobals(); document.body.innerHTML = ""; });

  it("takes the cash session page link to this page as returnTo when it is used", async () => {
    window.history.replaceState({}, "", `/cashier/payments/split-payment/index.html?billId=${BILL_ID}`);
    installFetchRouter([
      { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
      { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: unpaid },
      { test: (url) => url.includes("/cash-sessions/active"), status: 404, body: null },
      { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
      { test: (url) => url.endsWith("/refund-requests"), body: [] },
    ]);
    await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
    await vi.waitFor(() => expect(document.querySelector("#cash-state-notice a")).not.toBeNull());
    const link = document.querySelector("#cash-state-notice a");
    expect(link.textContent).toBe("Kasayı aç");
    link.addEventListener("click", (event) => event.preventDefault());
    link.click();
    const url = new URL(link.getAttribute("href"), "http://localhost");
    expect(url.pathname).toBe("/cashier/payments/cash-session/index.html");
    expect(url.searchParams.get("returnTo")).toBe(`/cashier/payments/split-payment/index.html?billId=${BILL_ID}`);
  });
});
