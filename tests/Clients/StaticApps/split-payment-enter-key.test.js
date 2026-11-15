import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const bill = (paid) => ({ billId: BILL_ID, payableAmount: 100, allocatedTotal: paid, remainingAmount: 100 - paid, allocations: [], unsettledPayment: null });

async function open() {
  let current = bill(0);
  let release;
  const gate = new Promise((resolveGate) => { release = resolveGate; });
  const fetchMock = installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", get body() { return current; } },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "66666666-6666-6666-6666-666666666666" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url) => url.endsWith("/show-on-display"), body: {} },
    { test: (url, init) => url.endsWith("/cash-tender") && init?.method === "POST", get body() { current = bill(40); return gate.then(() => ({ outcome: "Approved", approvedAmount: 40, changeAmount: 0 })); } },
  ]);
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.getElementById("submit-tender")?.disabled).toBe(false));
  return { fetchMock, release };
}

const tenders = (fetchMock) => fetchMock.mock.calls.filter(([url, init]) => url.endsWith("/cash-tender") && init?.method === "POST");
const type = (id, value) => {
  const input = document.getElementById(id);
  input.value = value;
  input.dispatchEvent(new Event("input", { bubbles: true }));
  return input;
};
const enter = (input) => input.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", bubbles: true, cancelable: true }));

describe("Enter in the payment fields adds the payment", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => { vi.unstubAllGlobals(); document.body.innerHTML = ""; });

  it.each(["amount-draft", "tendered-draft"])("sends one cash tender from %s and ignores a second Enter while it is in flight", async (field) => {
    const { fetchMock, release } = await open();
    const amount = type("amount-draft", "40");
    const target = field === "amount-draft" ? amount : type("tendered-draft", "50");

    enter(target);
    enter(document.getElementById(field) ?? target);
    expect(tenders(fetchMock)).toHaveLength(1);
    release();
    await vi.waitFor(() => expect(document.body.textContent).toContain("Kalan₺60,00"));
  });

  it("ignores other keys", async () => {
    const { fetchMock } = await open();
    const amount = type("amount-draft", "40");

    amount.dispatchEvent(new KeyboardEvent("keydown", { key: "a", bubbles: true, cancelable: true }));

    expect(tenders(fetchMock)).toHaveLength(0);
  });

  it("does nothing while the button is off (transfer not confirmed)", async () => {
    const { fetchMock } = await open();
    document.querySelector('[data-method="Eft"]').click();
    const amount = type("amount-draft", "40");

    enter(amount);

    expect(document.getElementById("submit-tender").disabled).toBe(true);
    expect(fetchMock.mock.calls.some(([url, init]) => url.endsWith("/tenders/") && init?.method === "POST")).toBe(false);
  });

  it("does nothing when the cash received is less than the amount", async () => {
    const { fetchMock } = await open();
    type("amount-draft", "40");
    const tendered = type("tendered-draft", "10");

    enter(tendered);

    expect(tenders(fetchMock)).toHaveLength(0);
  });
});
