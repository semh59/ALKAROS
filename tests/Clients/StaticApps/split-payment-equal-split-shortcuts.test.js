import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const bill = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };

async function open() {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: bill },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "66666666-6666-6666-6666-666666666666" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.getElementById("submit-tender")).not.toBeNull());
}

const people = (n) => document.querySelector(`[data-split-people="${n}"]`);

describe("equal split shortcuts beside the amount field", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => { vi.unstubAllGlobals(); document.body.innerHTML = ""; });

  it("offers 2, 3 and 4 person buttons outside the closed fold", async () => {
    await open();
    expect(document.getElementById("fold-split").open).toBe(false);
    expect([2, 3, 4].map((n) => people(n)?.textContent)).toEqual(["2 kişi", "3 kişi", "4 kişi"]);
    expect(people(2).closest("details")).toBeNull();
    expect(people(2).parentElement.getAttribute("aria-label")).toBe("Eşit bölme");
  });

  it.each([[2, "50.00"], [3, "33.33"], [4, "25.00"]])("%i people put one share into the amount", async (count, share) => {
    await open();
    people(count).click();
    expect(document.getElementById("amount-draft").value).toBe(share);
    expect(document.getElementById("payer-hint").textContent).toBe(`Ödeyen: Kişi 1`);
    expect(document.getElementById("split-count").value).toBe(String(count));
  });

  it("agrees with the manual Hesapla button", async () => {
    await open();
    const input = document.getElementById("split-count");
    input.value = "3";
    input.dispatchEvent(new Event("input", { bubbles: true }));
    document.getElementById("apply-split").click();
    const manual = document.getElementById("amount-draft").value;
    people(3).click();
    expect(document.getElementById("amount-draft").value).toBe(manual);
  });
});
