import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..");
const htmlPath = resolve(repoRoot, "src/Clients/Cashier/wwwroot/index.html");
const scriptPath = resolve(repoRoot, "src/Clients/Cashier/wwwroot/cashier-app.js");

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const PRODUCT_ID = "22222222-2222-2222-2222-222222222222";
const ORDER_ID = "33333333-3333-3333-3333-333333333333";

const WAITER_ID = "44444444-4444-4444-4444-444444444444";

function standardRoutes({
  draftStatus = 200, submitStatus = 200, staffStatus = 200, suggestedStatus = 200, waiterLoadStatus = 200,
} = {}) {
  return [
    {
      test: (url) => url.endsWith("/api/v1/auth/session/current"),
      body: { terminalId: TERMINAL_ID, displayName: "Test Kasiyer" },
    },
    {
      test: (url) => url.endsWith(`/api/v1/terminals/${TERMINAL_ID}/catalog`),
      body: [
        {
          productId: PRODUCT_ID,
          name: "Kola",
          currentPrice: 45,
          categoryId: "c1",
          categoryName: "İçecek",
          sku: "K1",
        },
      ],
    },
    {
      test: (url) => url.endsWith(`/api/v1/terminals/${TERMINAL_ID}/orders/staff`),
      status: staffStatus,
      body: [{ userId: WAITER_ID, displayName: "Ayşe Garson" }],
    },
    {
      test: (url) => url.endsWith(`/api/v1/terminals/${TERMINAL_ID}/orders/suggested-waiter`),
      status: suggestedStatus,
      body: { userId: WAITER_ID, displayName: "Ayşe Garson" },
    },
    {
      test: (url) => url.endsWith(`/api/v1/terminals/${TERMINAL_ID}/orders/waiter-load`),
      status: waiterLoadStatus,
      body: [{ userId: WAITER_ID, activeLoad: 3 }],
    },
    {
      test: (url, init) => url.endsWith(`/api/v1/terminals/${TERMINAL_ID}/orders/table-draft`) && init?.method === "POST",
      status: draftStatus,
      body: { orderId: ORDER_ID, tableId: "0", tableNumber: "KASA-1", status: "Draft", rowVersion: 1, totalAmount: 45, items: [] },
    },
    {
      test: (url, init) => url.endsWith(`/api/v1/terminals/${TERMINAL_ID}/orders/${ORDER_ID}/submit-draft`) && init?.method === "POST",
      status: submitStatus,
      body: { orderId: ORDER_ID, tableId: "0", tableNumber: "KASA-1", status: "Submitted", rowVersion: 2, totalAmount: 45, items: [] },
    },
    {
      test: (url, init) => url.endsWith(`/api/v1/terminals/${TERMINAL_ID}/orders/${ORDER_ID}/send-to-cashier`) && init?.method === "POST",
      body: {},
    },
  ];
}

async function startAppWithOneProductInTicket(routes) {
  const fetchMock = installFetchRouter(routes);
  vi.stubGlobal("alert", vi.fn());
  await loadApp(htmlPath, scriptPath);

  await vi.waitFor(() => {
    if (!document.querySelector(".pos-product-card")) throw new Error("catalog not rendered yet");
  });
  document.querySelector(".pos-product-card").click();

  return fetchMock;
}

describe("cashier-app.js", () => {
  beforeEach(() => {
    vi.resetModules();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("sends the ticket to the kitchen via table-draft then submit-draft, in that order", async () => {
    // Regression coverage for a Critical finding (2026-09-06, V1-RMD-106):
    // neither production client ever called submit-draft, so an order
    // never actually reached the kitchen even though the UI reported
    // success. This drives the real click handler end to end and asserts
    // both calls happen, in the right order, against the right paths.
    const fetchMock = await startAppWithOneProductInTicket(standardRoutes());

    document.getElementById("btnDispatchOrder").click();

    await vi.waitFor(() => {
      // session + catalog + staff + suggested-waiter + waiter-load + table-draft (submit-draft is the 7th)
      expect(fetchMock).toHaveBeenCalledTimes(6);
    });
    await vi.waitFor(() => {
      // ...then send-to-cashier releases KASA-1 (the 8th).
      expect(fetchMock).toHaveBeenCalledTimes(8);
    });

    const [draftUrl, draftInit] = fetchMock.mock.calls[5];
    const [submitUrl, submitInit] = fetchMock.mock.calls[6];
    expect(draftUrl).toContain("/orders/table-draft");
    expect(draftInit.method).toBe("POST");
    expect(submitUrl).toContain(`/orders/${ORDER_ID}/submit-draft`);
    expect(submitInit.method).toBe("POST");
    expect(alert).toHaveBeenCalledWith(expect.stringContaining("mutfağa iletildi"));
  });

  it("does not send a second request while the first dispatch is still in flight", async () => {
    // Regression coverage for the double-click guard (found 2026-09-05):
    // a rapid second click on the dispatch button must not double the
    // network calls.
    const fetchMock = await startAppWithOneProductInTicket(standardRoutes());

    const button = document.getElementById("btnDispatchOrder");
    button.click();
    button.click();
    button.click();

    await vi.waitFor(() => {
      expect(fetchMock).toHaveBeenCalledTimes(8);
    });
    // A short settle window to prove no extra calls trickle in from the
    // extra clicks before asserting the final count stays at 8.
    await new Promise((r) => setTimeout(r, 20));
    expect(fetchMock).toHaveBeenCalledTimes(8);
  });

  it("never shows the raw HTTP status code when the server rejects the draft", async () => {
    // UI_STYLE_GUIDE §3: raw HTTP status codes must never reach the user.
    const fetchMock = await startAppWithOneProductInTicket(standardRoutes({ draftStatus: 409 }));

    document.getElementById("btnDispatchOrder").click();

    await vi.waitFor(() => {
      expect(alert).toHaveBeenCalled();
    });
    const [message] = alert.mock.calls[0];
    expect(message).not.toMatch(/\b409\b/);
    expect(message).not.toMatch(/Hata:/);
  });

  it("reflects the terminal's real connectivity instead of a static badge", async () => {
    // Regression coverage for a Critical finding (2026-09-07): the badge
    // was static markup always reading "Çevrimiçi" with no navigator.onLine
    // check and no online/offline listener anywhere in the script.
    installFetchRouter(standardRoutes());
    vi.stubGlobal('navigator', { ...navigator, onLine: false });
    await loadApp(htmlPath, scriptPath);

    const pill = document.getElementById('connectivityPill');
    expect(pill.classList.contains('session-pill--offline')).toBe(true);
    expect(pill.classList.contains('session-pill--online')).toBe(false);
    expect(document.getElementById('connectivityLabel').textContent).toBe('Çevrimdışı');

    vi.stubGlobal('navigator', { ...navigator, onLine: true });
    window.dispatchEvent(new Event('online'));

    expect(pill.classList.contains('session-pill--online')).toBe(true);
    expect(pill.classList.contains('session-pill--offline')).toBe(false);
    expect(document.getElementById('connectivityLabel').textContent).toBe('Çevrimiçi');

    vi.stubGlobal('navigator', { ...navigator, onLine: false });
    window.dispatchEvent(new Event('offline'));

    expect(pill.classList.contains('session-pill--offline')).toBe(true);
    expect(document.getElementById('connectivityLabel').textContent).toBe('Çevrimdışı');
  });

  it("V1-RMD-205: pre-selects the system's suggested waiter in the picker", async () => {
    await startAppWithOneProductInTicket(standardRoutes());

    await vi.waitFor(() => {
      const select = document.getElementById("dispatchWaiterSelect");
      if (select.value !== WAITER_ID) throw new Error("suggestion not applied yet");
    });
    const select = document.getElementById("dispatchWaiterSelect");
    expect(select.querySelectorAll("option")).toHaveLength(2); // "Ben" + the one staff member
    expect(select.selectedOptions[0].textContent).toBe("Ayşe Garson (3 masa)");
  });

  it("V1-RMD-212: falls back to no load suffix when /orders/waiter-load fails", async () => {
    await startAppWithOneProductInTicket(standardRoutes({ waiterLoadStatus: 500 }));

    await vi.waitFor(() => {
      const select = document.getElementById("dispatchWaiterSelect");
      if (select.value !== WAITER_ID) throw new Error("suggestion not applied yet");
    });
    const select = document.getElementById("dispatchWaiterSelect");
    expect(select.selectedOptions[0].textContent).toBe("Ayşe Garson");
  });

  it("V1-RMD-205: sends assignedWaiterUserId when a waiter other than the cashier is selected", async () => {
    const fetchMock = await startAppWithOneProductInTicket(standardRoutes());
    await vi.waitFor(() => {
      if (document.getElementById("dispatchWaiterSelect").value !== WAITER_ID) throw new Error("not ready");
    });

    document.getElementById("btnDispatchOrder").click();

    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(8));
    const [, draftInit] = fetchMock.mock.calls[5];
    const body = JSON.parse(draftInit.body);
    expect(body.assignedWaiterUserId).toBe(WAITER_ID);
  });

  it("V1-RMD-205: omits assignedWaiterUserId when the cashier keeps \"Ben\" selected", async () => {
    const fetchMock = await startAppWithOneProductInTicket(standardRoutes());
    await vi.waitFor(() => {
      if (document.getElementById("dispatchWaiterSelect").value !== WAITER_ID) throw new Error("not ready");
    });
    const select = document.getElementById("dispatchWaiterSelect");
    select.value = "";
    select.dispatchEvent(new Event("change"));

    document.getElementById("btnDispatchOrder").click();

    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(8));
    const [, draftInit] = fetchMock.mock.calls[5];
    const body = JSON.parse(draftInit.body);
    expect(body.assignedWaiterUserId).toBeUndefined();
  });

  it("V1-RMD-205: dispatch still works when staff/suggested-waiter fail", async () => {
    const fetchMock = await startAppWithOneProductInTicket(
      standardRoutes({ staffStatus: 500, suggestedStatus: 500 }));

    document.getElementById("btnDispatchOrder").click();

    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(8));
    expect(alert).toHaveBeenCalledWith(expect.stringContaining("mutfağa iletildi"));
    const select = document.getElementById("dispatchWaiterSelect");
    expect(select.querySelectorAll("option")).toHaveLength(1); // only "Ben"
  });
});
