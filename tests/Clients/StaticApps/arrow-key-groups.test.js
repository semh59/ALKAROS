import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..", "..", "src/Clients/Cashier/wwwroot");
const TERMINAL = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const CATALOG = [
  { productId: "00000000-0000-0000-0000-000000000001", name: "Kola", unitPrice: 45, categoryCode: "drinks", categoryName: "İçecek", sku: "K1", isAvailable: true },
  { productId: "00000000-0000-0000-0000-000000000002", name: "İskender", unitPrice: 180, categoryCode: "food", categoryName: "Yemek", sku: "I1", isAvailable: true },
];

function press(node, key, modifiers = {}) {
  node.dispatchEvent(new KeyboardEvent("keydown", { key, bubbles: true, cancelable: true, ...modifiers }));
}

describe("arrow keys in tab and option groups", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("moves the selected category with the arrow keys, wraps, and keeps the focus on the selected tab", async () => {
    installFetchRouter([
      { test: (url) => url.endsWith("/auth/session/current"), body: { terminalId: TERMINAL, userId: "u1", displayName: "Ayşe" } },
      { test: (url) => url.endsWith("/catalog"), body: CATALOG },
      { test: (url) => url.endsWith("/orders/staff"), body: [] },
      { test: (url) => url.endsWith("/orders/suggested-waiter"), status: 404 },
      { test: (url) => url.endsWith("/orders/waiter-load"), body: [] },
    ]);
    await loadApp(resolve(root, "index.html"), resolve(root, "cashier-app.js"));
    await vi.waitFor(() => expect(document.querySelector(".tab-chip")).not.toBeNull());
    const selected = () => document.querySelector('.tab-chip[aria-selected="true"]');
    const names = () => [...document.querySelectorAll(".tab-chip")].map((tab) => tab.textContent.trim());
    const order = names();
    const tabStops = () => [...document.querySelectorAll(".tab-chip")].filter((tab) => tab.tabIndex === 0);
    expect(tabStops()).toEqual([selected()]);
    selected().focus();

    press(document.activeElement, "ArrowRight");
    expect(names().indexOf(selected().textContent.trim())).toBe(1);
    expect(tabStops()).toEqual([selected()]);
    expect(document.activeElement).toBe(selected());
    press(document.activeElement, "ArrowRight", { altKey: true });
    expect(names().indexOf(selected().textContent.trim())).toBe(1);
    press(document.activeElement, "ArrowUp");
    press(document.activeElement, "ArrowLeft");
    expect(selected().textContent.trim()).toBe(order[order.length - 1]);
    expect(document.activeElement).toBe(selected());
  });

  it("moves the payment method with the arrow keys and skips the disabled cash chip", async () => {
    window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
    installFetchRouter([
      { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL, userId: "u1" } },
      { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null } },
      { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
      { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
      { test: (url) => url.endsWith("/refund-requests"), body: [] },
      { test: (url) => url.endsWith("/show-on-display"), status: 204 },
    ]);
    await loadApp(resolve(root, "payments/split-payment/index.html"), resolve(root, "payments/split-payment/split-payment.js"));
    const chip = (method) => document.querySelector(`.sp-method-chip[data-method="${method}"]`);
    await vi.waitFor(() => expect(chip("Eft")).not.toBeNull());
    expect(chip("Cash").disabled).toBe(true);
    const checked = () => document.querySelector('.sp-method-chip[aria-checked="true"]')?.dataset.method;
    chip("BankCard").click();
    expect(document.activeElement).toBe(chip("BankCard"));

    press(chip("BankCard"), "ArrowRight", { ctrlKey: true });
    expect(checked()).toBe("BankCard");
    press(chip("BankCard"), "ArrowRight");
    expect(checked()).toBe("Eft");
    press(document.activeElement, "ArrowDown");
    expect(checked()).toBe("Account");
    press(document.activeElement, "ArrowRight");
    expect(checked()).toBe("BankCard");
    press(document.activeElement, "ArrowLeft");
    expect(checked()).toBe("Account");
    expect(document.activeElement).toBe(chip("Account"));
  });
});
