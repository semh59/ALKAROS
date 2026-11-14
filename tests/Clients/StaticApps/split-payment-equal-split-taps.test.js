import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
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
const SESSION_ID = "66666666-6666-6666-6666-666666666666";

// Reaching the payment page from the till: "Bekleyen hesaplar", then the bill's card.
const TAPS_TO_REACH_THE_PAGE = 2;
const MAX_TAPS = 8;

const bill = (paid) => ({ billId: BILL_ID, payableAmount: 100, allocatedTotal: paid, remainingAmount: 100 - paid, allocations: [], unsettledPayment: null });

describe("split-payment.js the most common split: two people, one cash and one transfer", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("is paid within the tap limit, counted from the till's own bill list", async () => {
    let current = bill(0);
    const fetchMock = installFetchRouter([
      { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
      { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", get body() { return current; } },
      { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: SESSION_ID } },
      { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
      { test: (url) => url.endsWith("/refund-requests"), body: [] },
      { test: (url) => url.endsWith("/show-on-display"), body: {} },
      { test: (url) => url.endsWith("/split-design"), status: 404 },
      { test: (url, init) => url.endsWith("/cash-tender") && init?.method === "POST", get body() { current = bill(50); return { outcome: "Approved", approvedAmount: 50, changeAmount: 0 }; } },
      { test: (url, init) => url.endsWith("/tenders/") && init?.method === "POST", get body() { current = bill(100); return { outcome: "Approved", approvedAmount: 50 }; } },
    ]);
    window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect(document.getElementById("submit-tender")).not.toBeNull());

    let taps = TAPS_TO_REACH_THE_PAGE;
    const tap = (element) => { taps += 1; element.click(); };
    const settle = () => vi.waitFor(() => expect(document.getElementById("submit-tender")?.disabled).toBe(false));

    expect(document.querySelector('[data-method="Cash"]').getAttribute("aria-checked")).toBe("true");
    tap(document.getElementById("fold-split-summary"));
    await vi.waitFor(() => expect(document.getElementById("fold-split").open).toBe(true));
    tap(document.getElementById("apply-split"));
    expect(document.getElementById("amount-draft").value).toBe("50.00");
    tap(document.getElementById("submit-tender"));
    await vi.waitFor(() => expect(document.body.textContent).toContain("Kalan₺50,00"));
    await settle();
    tap(document.querySelector('[data-method="Eft"]'));
    tap(document.getElementById("eft-confirm"));
    tap(document.getElementById("submit-tender"));

    await vi.waitFor(() => expect(document.body.textContent).toContain("Hesap Ödendi"));
    const sent = (suffix) => JSON.parse(fetchMock.mock.calls.find(([url, init]) => url.endsWith(suffix) && init?.method === "POST")[1].body);
    expect(sent("/cash-tender").AmountDue).toBe(50);
    expect(sent("/tenders/").Amount).toBe(50);
    expect(taps).toBeLessThanOrEqual(MAX_TAPS);
  });
});
