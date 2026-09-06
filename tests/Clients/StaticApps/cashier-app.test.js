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

function standardRoutes({ draftStatus = 200, submitStatus = 200 } = {}) {
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
      test: (url, init) => url.endsWith(`/api/v1/terminals/${TERMINAL_ID}/orders/table-draft`) && init?.method === "POST",
      status: draftStatus,
      body: { orderId: ORDER_ID, tableId: "0", tableNumber: "KASA-1", status: "Draft", rowVersion: 1, totalAmount: 45, items: [] },
    },
    {
      test: (url, init) => url.endsWith(`/api/v1/terminals/${TERMINAL_ID}/orders/${ORDER_ID}/submit-draft`) && init?.method === "POST",
      status: submitStatus,
      body: { orderId: ORDER_ID, tableId: "0", tableNumber: "KASA-1", status: "Submitted", rowVersion: 2, totalAmount: 45, items: [] },
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
      expect(fetchMock).toHaveBeenCalledTimes(3); // session + catalog + table-draft (submit-draft is the 4th)
    });
    await vi.waitFor(() => {
      expect(fetchMock).toHaveBeenCalledTimes(4);
    });

    const [draftUrl, draftInit] = fetchMock.mock.calls[2];
    const [submitUrl, submitInit] = fetchMock.mock.calls[3];
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
      expect(fetchMock).toHaveBeenCalledTimes(4);
    });
    // A short settle window to prove no extra calls trickle in from the
    // extra clicks before asserting the final count stays at 4.
    await new Promise((r) => setTimeout(r, 20));
    expect(fetchMock).toHaveBeenCalledTimes(4);
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
});
