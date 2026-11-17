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

const unpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };
const settled = { ...unpaid, allocatedTotal: 100, remainingAmount: 0 };
const partial = { ...unpaid, allocatedTotal: 50, remainingAmount: 50 };
const NOTICE = "Çekmece açılamadı, elle açın.";

async function open({ tenders, afterTender = [settled], statuses = [200] } = {}) {
  let current = unpaid;
  const bodies = [...tenders];
  const afters = [...afterTender];
  const codes = [...statuses];
  const routeSet = [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", get body() { return current; } },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: SESSION_ID } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    {
      test: (url, init) => url.endsWith("/cash-tender") && init?.method === "POST",
      get status() { return codes.length > 1 ? codes.shift() : codes[0]; },
      get body() { current = afters.length > 1 ? afters.shift() : afters[0]; return bodies.length > 1 ? bodies.shift() : bodies[0]; },
    },
  ];
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  installFetchRouter(routeSet);
  await loadApp(htmlPath, scriptPath);
}

const app = () => document.getElementById("app");
const notice = () => document.getElementById("drawer-notice");

async function payCash(amount) {
  await vi.waitFor(() => expect(document.getElementById("submit-tender")).not.toBeNull());
  document.getElementById("amount-draft").value = String(amount);
  document.getElementById("amount-draft").dispatchEvent(new Event("input", { bubbles: true }));
  document.getElementById("submit-tender").click();
}

describe("split-payment.js drawer-not-opened notice", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("says calmly that the drawer did not open, on the paid screen, without an error box", async () => {
    await open({ tenders: [{ approvedAmount: 100, changeAmount: 0, drawerOpened: false }] });

    await payCash(100);

    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Ödendi"));
    expect(notice()).not.toBeNull();
    expect(notice().textContent).toBe(NOTICE);
    expect(notice().getAttribute("role")).toBe("status");
    expect(app().querySelector('[role="alert"]')).toBeNull();
    expect(app().querySelector(".sp-alert-danger")).toBeNull();
  });

  it.each([
    ["the drawer opened", { drawerOpened: true }],
    ["the drawer is off or the answer is a replay", { drawerOpened: null }],
    ["the answer has no drawer field", {}],
  ])("shows nothing when %s", async (_name, extra) => {
    await open({ tenders: [{ approvedAmount: 100, changeAmount: 0, ...extra }] });

    await payCash(100);

    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Ödendi"));
    expect(notice()).toBeNull();
    expect(app().textContent).not.toContain("Çekmece");
  });

  it("keeps the notice after a part payment and drops it when the next payment opens the drawer", async () => {
    await open({
      tenders: [{ approvedAmount: 50, changeAmount: 0, drawerOpened: false }, { approvedAmount: 50, changeAmount: 0, drawerOpened: true }],
      afterTender: [partial, settled],
    });

    await payCash(50);
    await vi.waitFor(() => expect(notice()).not.toBeNull());
    expect(notice().textContent).toBe(NOTICE);
    expect(notice().getAttribute("role")).toBe("status");
    expect(app().textContent).not.toContain("Hesap Ödendi");

    await payCash(50);
    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Ödendi"));
    expect(notice()).toBeNull();
  });

  it("drops the old notice when the next payment fails, so it never sits beside an error box", async () => {
    await open({
      tenders: [{ approvedAmount: 50, changeAmount: 0, drawerOpened: false }, { error: { code: "TENDER_REJECTED" } }],
      afterTender: [partial],
      statuses: [200, 422],
    });

    await payCash(50);
    await vi.waitFor(() => expect(notice()).not.toBeNull());

    await payCash(50);

    await vi.waitFor(() => expect(app().querySelector('.sp-alert-danger[role="alert"]')).not.toBeNull());
    expect(notice()).toBeNull();
  });
});
