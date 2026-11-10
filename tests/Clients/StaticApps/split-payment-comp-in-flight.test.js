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
const pending = { status: "Pending", billId: BILL_ID, adjustmentId: null, summary: null, grantId: "g1" };

async function open() {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: unpaid },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url, init) => url.endsWith("/comp") && init?.method === "POST", status: 202, body: pending, delayMs: 80 },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.getElementById("submit-comp")).not.toBeNull());
  return fetchMock;
}

const type = (id, value) => {
  const input = document.getElementById(id);
  input.value = value;
  input.dispatchEvent(new Event("input", { bubbles: true }));
};
const compIds = ["comp-reason", "comp-amount", "comp-note", "comp-all", "submit-comp"];

describe("split-payment.js comp while the request is in flight", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("locks every comp field and button until the answer arrives, so the sent amount cannot be edited into a whole-bill comp", async () => {
    const fetchMock = await open();
    type("comp-amount", "20");

    document.getElementById("submit-comp").click();

    expect(compIds.map((id) => document.getElementById(id).disabled)).toEqual([true, true, true, true, true]);
    await vi.waitFor(() => expect(document.getElementById("submit-comp").textContent).toBe("Onay durumunu kontrol et"));
    expect(compIds.map((id) => document.getElementById(id).disabled)).toEqual([false, false, false, false, false]);
    const sent = fetchMock.mock.calls.filter(([url, init]) => url.endsWith("/comp") && init?.method === "POST");
    expect(sent).toHaveLength(1);
    expect(JSON.parse(sent[0][1].body).Amount).toBe(20);
  });
});
