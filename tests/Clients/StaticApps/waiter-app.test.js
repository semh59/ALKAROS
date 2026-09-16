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
const PRODUCT_ID_2 = "55555555-5555-5555-5555-555555555555";
const TABLE_ID = "44444444-4444-4444-4444-444444444444";
const ORDER_ID = "33333333-3333-3333-3333-333333333333";

// V1-RMD-206: rebuilt against the CURRENT app. The previous version of this
// file predated the 17-step JS modularization (V1-WTR-037..053) and the
// tables/menu screen rewrite - it asserted on a ".table-card"/".product-card"
// markup and "#btnOpenOrderModal"/"#btnSendKitchen" button ids that do not
// exist anywhere in this codebase anymore (grep-confirmed), and on a plain
// window.alert() the app replaced with an in-page toast (js/toast.js) a
// while ago. Every one of its four tests failed for that reason alone,
// independent of whatever regression each was originally written to guard.
function standardRoutes({ draftStatus = 200, submitStatus = 200 } = {}) {
  return [
    { test: (url) => url.includes("/api/v1/auth/session?"), body: { userId: "u1", name: "Garson Ahmet" } },
    { test: (url) => url.includes("/runtime-configuration"), body: { garsonFeatures: {} } },
    { test: (url) => url.includes("/table-management/zones"), body: [] },
    { test: (url) => url.includes("/catalog"), body: [{ productId: PRODUCT_ID, name: "Kola", unitPrice: 45, categoryCode: "c1", categoryName: "İçecek" }] },
    {
      test: (url) => url.includes("/table-management/tables"),
      body: [{ tableId: TABLE_ID, tableNumber: "M-05", capacity: 4, zoneId: "z1", status: "Available" }],
    },
    // A 404 here (no order yet) is what openTable() reads as "empty table" -
    // its own real-world branch straight to the menu screen, no order to load.
    { test: (url) => url.includes(`/orders/table/${TABLE_ID}`), status: 404, body: {} },
    { test: (url) => url.includes("/orders/pending"), body: [] },
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
  await loadApp(htmlPath, scriptPath);

  await vi.waitFor(() => {
    if (!document.querySelector(".table[data-table]")) throw new Error("tables not rendered yet");
  });
  document.querySelector(".table[data-table]").click();

  await vi.waitFor(() => {
    if (!document.querySelector(".product[data-product]")) throw new Error("products not rendered yet");
  });
  document.querySelector(".product[data-product]").click();

  return fetchMock;
}

function lastToastText() {
  const nodes = document.querySelectorAll("#toasts .toast-text");
  return nodes.length ? nodes[nodes.length - 1].textContent : null;
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

    document.getElementById("btnSendFromMenu").click();

    await vi.waitFor(() => {
      const calls = fetchMock.mock.calls.slice(beforeSend);
      if (!calls.some(([url]) => url.includes("/orders/table-draft"))) throw new Error("table-draft not called yet");
      if (!calls.some(([url]) => url.includes(`/orders/${ORDER_ID}/submit-draft`))) throw new Error("submit-draft not called yet");
    });

    const calls = fetchMock.mock.calls.slice(beforeSend);
    const draftCall = calls.find(([url]) => url.includes("/orders/table-draft"));
    const submitCall = calls.find(([url]) => url.includes(`/orders/${ORDER_ID}/submit-draft`));
    expect(draftCall[1].method).toBe("POST");
    expect(submitCall[1].method).toBe("POST");
    // submit-draft only makes sense after table-draft returned an orderId.
    expect(calls.indexOf(draftCall)).toBeLessThan(calls.indexOf(submitCall));
    await vi.waitFor(() => {
      expect(lastToastText()).toContain("mutfağa gönderildi");
    });
  });

  it("does not send a second request while the first dispatch is still in flight", async () => {
    const fetchMock = await startAppWithOneProductInCart(standardRoutes());
    const beforeSend = fetchMock.mock.calls.length;

    const button = document.getElementById("btnSendFromMenu");
    button.click();
    button.click();
    button.click();

    await vi.waitFor(() => {
      const calls = fetchMock.mock.calls.slice(beforeSend);
      if (!calls.some(([url]) => url.includes(`/orders/${ORDER_ID}/submit-draft`))) throw new Error("not sent yet");
    });
    const afterFirstSend = fetchMock.mock.calls.length;
    // A short settle window to prove no extra calls trickle in from the
    // extra clicks before asserting the count stays put.
    await new Promise((r) => setTimeout(r, 20));
    expect(fetchMock.mock.calls.length).toBe(afterFirstSend);
  });

  it("never shows the raw HTTP status code when the server rejects the draft", async () => {
    await startAppWithOneProductInCart(standardRoutes({ draftStatus: 409 }));

    document.getElementById("btnSendFromMenu").click();

    await vi.waitFor(() => {
      if (!lastToastText()) throw new Error("no toast yet");
    });
    const shown = lastToastText();
    expect(shown).not.toMatch(/\b409\b/);
    expect(shown).not.toMatch(/Hata:/);
  });

  it("V1-RMD-213: keeps an item added while a send is still in flight instead of losing it silently", async () => {
    // Regression coverage for a Critical finding (2026-09-16, independent
    // multi-agent audit): sendDraft() used to hold a live reference to
    // state.draft; a line added mid-await (the network round trip to
    // table-draft/submit-draft) got swept up by removeSentDraftLines as
    // "already sent" and vanished, even though it never reached the server.
    const routes = standardRoutes();
    routes.find((r) => r.test("http://test/catalog")).body = [
      { productId: PRODUCT_ID, name: "Kola", unitPrice: 45, categoryCode: "c1", categoryName: "İçecek" },
      { productId: PRODUCT_ID_2, name: "Ayran", unitPrice: 20, categoryCode: "c1", categoryName: "İçecek" },
    ];
    routes.find((r) => r.test("http://test/orders/table-draft", { method: "POST" })).delayMs = 30;

    const fetchMock = await startAppWithOneProductInCart(routes);

    document.getElementById("btnSendFromMenu").click();

    // While table-draft is still in flight (delayMs above), touch a second,
    // distinct product - this is the mid-send addition the bug lost.
    await vi.waitFor(() => {
      const products = document.querySelectorAll(".product[data-product]");
      if (products.length < 2) throw new Error("second product not rendered yet");
    });
    document.querySelectorAll(".product[data-product]")[1].click();

    await vi.waitFor(() => {
      if (!fetchMock.mock.calls.some(([url]) => url.includes(`/orders/${ORDER_ID}/submit-draft`))) {
        throw new Error("send not finished yet");
      }
    });

    // The first product was sent and removed; the second, added mid-send,
    // must still be sitting in the cart - not silently dropped.
    expect(document.getElementById("cartCount").textContent).not.toBe("0");
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
