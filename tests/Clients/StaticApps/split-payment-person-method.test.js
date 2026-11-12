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
const unpaid = { billId: BILL_ID, payableAmount: 190, allocatedTotal: 0, remainingAmount: 190, allocations: [], unsettledPayment: null };

async function open() {
  const routeSet = [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: unpaid },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url) => url.endsWith("/show-on-display"), body: {} },
    { test: (url) => url.endsWith("/split-design"), body: { allocations: [{ ownerId: "o1", amount: 65 }, { ownerId: "o2", amount: 125 }] } },
    { test: (url) => url.endsWith("/split-design/owners"), body: [{ ownerId: "o1", label: "Kişi 1" }, { ownerId: "o2", label: "Kişi 2" }] },
  ];
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  installFetchRouter(routeSet);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect(document.querySelectorAll("[data-share]")).toHaveLength(2));
}

const active = () => [...document.querySelectorAll("[data-method]")].filter((chip) => chip.getAttribute("aria-checked") === "true").map((chip) => chip.getAttribute("data-method"));

afterEach(() => { document.body.innerHTML = ""; });

describe("a person's saved share only fills the amount", () => {
  it("keeps the chosen payment method when a person's share is tapped", async () => {
    await open();
    document.querySelector('[data-method="Eft"]').click();
    expect(active()).toEqual(["Eft"]);

    document.querySelector('[data-share="1"]').click();

    expect(document.getElementById("amount-draft").value).toBe("125.00");
    expect(active()).toEqual(["Eft"]);
    expect(document.getElementById("method-hint")).toBeNull();
  });

  it("keeps the default cash method when the first person's share is tapped", async () => {
    await open();

    document.querySelector('[data-share="0"]').click();

    expect(active()).toEqual(["Cash"]);
  });
});
