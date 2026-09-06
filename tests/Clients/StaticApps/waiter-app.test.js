import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..");
const htmlPath = resolve(repoRoot, "src/Clients/WaiterPwa/wwwroot/index.html");
const scriptPath = resolve(repoRoot, "src/Clients/WaiterPwa/wwwroot/waiter-app.js");

const PRODUCT_ID = "22222222-2222-2222-2222-222222222222";
const TABLE_ID = "44444444-4444-4444-4444-444444444444";
const ORDER_ID = "33333333-3333-3333-3333-333333333333";

function standardRoutes({ draftStatus = 200, submitStatus = 200 } = {}) {
  return [
    { test: (url) => url.includes("/api/v1/auth/session?"), body: { userId: "u1", name: "Garson Ahmet" } },
    { test: (url) => url.includes("/table-management/zones"), body: [] },
    { test: (url) => url.includes("/catalog?category=all"), body: [] },
    {
      test: (url) => url.includes("/catalog") && !url.includes("category="),
      body: [{ productId: PRODUCT_ID, productName: "Kola", currentPrice: 45, categoryId: "c1" }],
    },
    {
      test: (url) => url.includes("/table-management/tables"),
      body: [{ tableId: TABLE_ID, tableNumber: "M-05", capacity: 4, zoneId: "z1", currentStatus: "Available" }],
    },
    {
      test: (url, init) => url.includes("/orders/table-draft") && init?.method === "POST",
      status: draftStatus,
      body: { orderId: ORDER_ID, tableId: TABLE_ID, tableNumber: "M-05", status: "Draft", rowVersion: 1, totalAmount: 45, items: [] },
    },
    {
      test: (url, init) => url.includes(`/orders/${ORDER_ID}/submit-draft`) && init?.method === "POST",
      status: submitStatus,
      body: { orderId: ORDER_ID, tableId: TABLE_ID, tableNumber: "M-05", status: "Submitted", rowVersion: 2, totalAmount: 45, items: [] },
    },
  ];
}

async function startAppWithOneProductInCart(routes) {
  const fetchMock = installFetchRouter(routes);
  vi.stubGlobal("alert", vi.fn());
  await loadApp(htmlPath, scriptPath);

  await vi.waitFor(() => {
    if (!document.querySelector(".table-card")) throw new Error("tables not rendered yet");
  });
  document.querySelector(".table-card").click();
  document.getElementById("btnOpenOrderModal").click();

  await vi.waitFor(() => {
    if (!document.querySelector(".product-card")) throw new Error("products not rendered yet");
  });
  document.querySelector(".product-card").click();

  return fetchMock;
}

describe("waiter-app.js", () => {
  beforeEach(() => {
    vi.resetModules();
    localStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("sends the cart to the kitchen via table-draft then submit-draft, in that order", async () => {
    // Regression coverage for a Critical finding (2026-09-06, V1-RMD-106):
    // neither production client ever called submit-draft, so an order
    // never actually reached the kitchen even though the UI reported
    // success.
    const fetchMock = await startAppWithOneProductInCart(standardRoutes());
    const beforeSend = fetchMock.mock.calls.length;

    document.getElementById("btnSendKitchen").click();

    await vi.waitFor(() => {
      expect(fetchMock.mock.calls.length).toBeGreaterThanOrEqual(beforeSend + 2);
    });

    const calls = fetchMock.mock.calls.slice(beforeSend);
    const draftCall = calls.find(([url]) => url.includes("/orders/table-draft"));
    const submitCall = calls.find(([url]) => url.includes(`/orders/${ORDER_ID}/submit-draft`));
    expect(draftCall, "table-draft was never called").toBeTruthy();
    expect(submitCall, "submit-draft was never called").toBeTruthy();
    expect(draftCall[1].method).toBe("POST");
    expect(submitCall[1].method).toBe("POST");
    // submit-draft only makes sense after table-draft returned an orderId.
    expect(calls.indexOf(draftCall)).toBeLessThan(calls.indexOf(submitCall));
    expect(alert).toHaveBeenCalledWith(expect.stringContaining("mutfağa iletildi"));
  });

  it("does not send a second request while the first dispatch is still in flight", async () => {
    const fetchMock = await startAppWithOneProductInCart(standardRoutes());
    const beforeSend = fetchMock.mock.calls.length;

    const button = document.getElementById("btnSendKitchen");
    button.click();
    button.click();
    button.click();

    await vi.waitFor(() => {
      expect(fetchMock.mock.calls.length).toBeGreaterThanOrEqual(beforeSend + 2);
    });
    await new Promise((r) => setTimeout(r, 20));
    expect(fetchMock.mock.calls.length).toBe(beforeSend + 2);
  });

  it("never shows the raw HTTP status code when the server rejects the draft", async () => {
    const fetchMock = await startAppWithOneProductInCart(standardRoutes({ draftStatus: 409 }));

    document.getElementById("btnSendKitchen").click();

    await vi.waitFor(() => {
      expect(alert).toHaveBeenCalled();
    });
    const messages = alert.mock.calls.map(([m]) => m);
    for (const message of messages) {
      expect(message).not.toMatch(/\b409\b/);
      expect(message).not.toMatch(/Hata:/);
    }
  });

  it("shows a Turkish message, not the browser's own error, when the login request itself fails", async () => {
    // Regression coverage for a Low finding (2026-09-06): a network-level
    // fetch failure (offline, DNS, TLS) throws before a response exists,
    // and its message used to be shown to the user verbatim.
    installFetchRouter([
      { test: (url) => url.includes("/api/v1/auth/session?"), status: 401, body: {} },
    ]);
    await loadApp(htmlPath, scriptPath);

    await vi.waitFor(() => {
      if (document.getElementById("loginOverlay").hidden) throw new Error("login overlay not shown yet");
    });

    vi.stubGlobal(
      "fetch",
      vi.fn(async (url) => {
        if (String(url).includes("/api/v1/auth/login")) {
          throw new TypeError("Failed to fetch");
        }
        throw new Error(`Unexpected fetch to ${url}`);
      }),
    );

    document.getElementById("loginUsername").value = "ahmet";
    document.getElementById("loginPassword").value = "sifre123";
    document.getElementById("loginForm").dispatchEvent(new Event("submit", { cancelable: true }));

    await vi.waitFor(() => {
      if (document.getElementById("loginError").hidden) throw new Error("login error not shown yet");
    });
    const shown = document.getElementById("loginError").textContent;
    expect(shown).not.toMatch(/Failed to fetch/i);
    expect(shown).toContain("Sunucuya ulaşılamadı");
  });
});
