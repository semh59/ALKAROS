import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const pageDir = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const summary = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null, cardTerminalConfigured: false };

async function pickCustomer(creditLimit) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: "11111111-1111-1111-1111-111111111111", userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: summary },
    { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url) => url.endsWith("/show-on-display"), status: 204 },
    { test: (url) => url.includes("/customers"), body: [{ customerId: "c1", name: "Ayşe", phoneMasked: null, balance: 0, availableCredit: creditLimit, creditLimit }] },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.querySelector(".sp-title")).not.toBeNull());
  document.querySelector('[data-method="Account"]').click();
  await vi.waitFor(() => expect(document.querySelector(".sp-customer")).not.toBeNull());
  document.querySelector(".sp-customer").click();
}

describe("split-payment.js customer with no credit limit", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("says credit cannot be written and that only a manager sets the limit", async () => {
    await pickCustomer(0);
    expect(document.getElementById("credit-limit-note").textContent)
      .toBe("Bu müşterinin kredi limiti 0; hesaba yazılamaz. Limiti yalnız yönetici belirleyebilir.");
  });

  it("shows no note when the customer has a limit", async () => {
    await pickCustomer(500);
    expect(document.getElementById("credit-limit-note")).toBeNull();
  });
});
