import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";

async function open({ payable = 200, adjustments = [] } = {}) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: { billId: BILL_ID, payableAmount: payable, allocatedTotal: 0, remainingAmount: payable, allocations: [], unsettledPayment: null } },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "66666666-6666-6666-6666-666666666666" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments, summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url) => url.endsWith("/show-on-display"), body: {} },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.getElementById("tip-amount")).not.toBeNull());
  return fetchMock;
}

const tipButton = (percent) => document.querySelector(`[data-tip-percent="${percent}"]`);
const discountButton = (percent) => document.querySelector(`[data-discount-percent="${percent}"]`);
const posts = (fetchMock) => fetchMock.mock.calls.filter(([url, init]) => init?.method === "POST" && !url.endsWith("/show-on-display"));

describe("tip and discount percentage buttons", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => { vi.unstubAllGlobals(); document.body.innerHTML = ""; });

  it.each([[10, "20.00"], [15, "30.00"], [20, "40.00"]])("tip %i percent fills %s and sends nothing", async (percent, amount) => {
    const fetchMock = await open();
    expect(tipButton(percent).textContent).toBe(`%${percent}`);
    tipButton(percent).click();

    expect(document.getElementById("tip-amount").value).toBe(amount);
    expect(posts(fetchMock)).toHaveLength(0);
  });

  it("computes the tip from the bill total without the tips already added", async () => {
    await open({ payable: 220, adjustments: [{ adjustmentId: "t1", adjustmentType: "Tip", amount: 20, isDeduction: false }] });
    tipButton(10).click();

    expect(document.getElementById("tip-amount").value).toBe("20.00");
  });

  it.each([5, 10, 20])("discount %i percent selects percentage, fills the value and sends nothing", async (percent) => {
    const fetchMock = await open();
    document.getElementById("discount-calc-type").value = "FixedAmount";
    document.getElementById("discount-calc-type").dispatchEvent(new Event("change", { bubbles: true }));
    expect(discountButton(percent).textContent).toBe(`%${percent}`);
    discountButton(percent).click();

    expect(document.getElementById("discount-calc-type").value).toBe("Percentage");
    expect(document.getElementById("discount-value").value).toBe(String(percent));
    expect(document.getElementById("submit-discount").textContent).toBe("İndirim uygula");
    expect(posts(fetchMock)).toHaveLength(0);
  });

  it("keeps the discount reason the cashier chose", async () => {
    await open();
    const reason = document.getElementById("discount-reason");
    const other = [...reason.options].map((option) => option.value).at(-1);
    reason.value = other;
    reason.dispatchEvent(new Event("change", { bubbles: true }));
    discountButton(10).click();

    expect(document.getElementById("discount-reason").value).toBe(other);
  });
});
