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

  describe("V1-RMD-330: offline mode survives a failed set-up and a busy flush", () => {
    let register;

    beforeEach(() => {
      register = vi.fn();
      Object.defineProperty(window, "isSecureContext", { value: true, configurable: true });
      Object.defineProperty(navigator, "serviceWorker", { value: { register }, configurable: true });
    });

    afterEach(() => {
      delete window.isSecureContext;
      delete navigator.serviceWorker;
    });

    const ribbon = () => document.getElementById("ribbonText").textContent;

    it("retries a failed service worker registration when the connection returns and clears the warning", async () => {
      register.mockRejectedValueOnce(new TypeError("Failed to fetch sw.js")).mockResolvedValue({});
      await startAppWithOneProductInCart(standardRoutes());
      await vi.waitFor(() => expect(ribbon()).toContain("kurulamadı"));

      window.dispatchEvent(new Event("offline"));
      // A lost connection is what the waiter sees first, whatever the service worker's state.
      expect(ribbon()).toContain("Bağlantı yok");

      window.dispatchEvent(new Event("online"));
      await vi.waitFor(() => expect(register).toHaveBeenCalledTimes(2));
      await vi.waitFor(() => expect(ribbon()).toMatch(/^Bağlı/));
    });

    it("runs the queue again at once when a trigger arrives during a flush that fails", async () => {
      register.mockResolvedValue({});
      let submits = 0;
      const routes = standardRoutes();
      const submitRoute = routes.find((r) => r.test(`http://test/orders/${ORDER_ID}/submit-draft`, { method: "POST" }));
      submitRoute.delayMs = 150;
      Object.defineProperty(submitRoute, "status", { get: () => (submits++ === 0 ? 503 : 200), configurable: true });
      const fetchMock = await startAppWithOneProductInCart(routes);
      window.dispatchEvent(new Event("offline"));
      document.getElementById("btnSendFromMenu").click();
      await vi.waitFor(() => expect(lastToastText()).toContain("kuyruğa alındı"));

      window.dispatchEvent(new Event("online"));
      await vi.waitFor(() => {
        if (!fetchMock.mock.calls.some(([url]) => url.includes("/submit-draft"))) throw new Error("flush not started");
      });
      // The connection "returns again" while the first (failing) flush is still waiting for the server.
      window.dispatchEvent(new Event("online"));

      // Well under the 15 s retry timer: the remembered trigger sends the round right after the failure.
      await vi.waitFor(() => {
        expect(JSON.parse(localStorage.getItem("alkaros_waiter_offline_queue") || "[]")).toHaveLength(0);
      }, { timeout: 3000 });
      expect(submits).toBe(2);
    });
  });
});

describe("waiter menu when the catalog cannot be read", () => {
  beforeEach(() => {
    vi.resetModules();
    localStorage.clear();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("says the menu is empty, without a retry, when the server sends no product", async () => {
    const routes = standardRoutes();
    routes.find((route) => route.test("http://test/catalog")).body = [];
    installFetchRouter(routes);
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => {
      if (!document.querySelector(".table[data-table]")) throw new Error("tables not rendered yet");
    });

    document.querySelector(".table[data-table]").click();

    await vi.waitFor(() => expect(document.getElementById("productList").textContent).toContain("Menüde ürün yok."));
    expect(document.querySelector("[data-retry-catalog]")).toBeNull();
  });

  it("says the menu could not be fetched, offers a retry and shows the menu once it loads", async () => {
    let catalogCalls = 0;
    const routes = standardRoutes();
    const catalogRoute = routes.find((route) => route.test("http://test/catalog"));
    Object.defineProperty(catalogRoute, "status", { get: () => (catalogCalls++ === 0 ? 500 : 200), configurable: true });
    installFetchRouter(routes);
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => {
      if (!document.querySelector(".table[data-table]")) throw new Error("tables not rendered yet");
    });

    document.querySelector(".table[data-table]").click();
    await vi.waitFor(() => expect(document.getElementById("productList").textContent).toContain("Menü alınamadı."));

    document.querySelector("[data-retry-catalog]").click();

    await vi.waitFor(() => expect(document.querySelector(".product[data-product]")).not.toBeNull());
  });
});

const OFFLINE_ACTION = {
  idempotencyKey: "k-1", permissionCode: "bills.void", requesterUserId: "u1", requesterRoleCode: "waiter", reasonCode: "CustomerChange",
  amount: 45, offlineAuthorizedAt: "2026-11-06T10:00:00.000Z", subjectType: "OrderItem", subjectId: "i1",
};
const OFFLINE_BUDGET = { budgetId: "b1", roleCode: "waiter", userId: "u1", expiresAt: "2999-01-01T00:00:00Z", lines: [], used: {} };

describe("offline authority budget and queued actions", () => {
  beforeEach(() => {
    vi.resetModules();
    localStorage.clear();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    delete globalThis.signalR;
    delete document.visibilityState;
    document.body.innerHTML = "";
  });

  const reconcileRoute = { test: (url, init) => url.includes("/offline-reconciliation") && init?.method === "POST", body: { summary: { brief: "Uzlaştırma tamam." } } };
  const reconcileCalls = (fetchMock) => fetchMock.mock.calls.filter(([url]) => url.includes("/offline-reconciliation"));

  function withQueuedAction() {
    localStorage.setItem("alkaros_waiter_offline_budget", JSON.stringify(OFFLINE_BUDGET));
    localStorage.setItem("alkaros_waiter_offline_actions", JSON.stringify([OFFLINE_ACTION]));
  }

  async function startSignedIn() {
    const fetchMock = installFetchRouter([reconcileRoute, ...standardRoutes()]);
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => {
      if (!document.querySelector(".table[data-table]")) throw new Error("tables not rendered yet");
    });
    return fetchMock;
  }

  it("keeps the budget the server issues at sign-in", async () => {
    const routes = [
      { test: (url) => url.includes("/api/v1/auth/session?"), status: 401, body: {} },
      {
        test: (url, init) => url.includes("/api/v1/auth/login") && init?.method === "POST",
        body: { userId: "u1", name: "Garson Ahmet", pinSet: false, offlineBudget: { budgetId: "b1", roleCode: "waiter", expiresAt: "2999-01-01T00:00:00Z", lines: [] } },
      },
      ...standardRoutes().slice(1),
    ];
    installFetchRouter(routes);
    await loadApp(htmlPath, scriptPath);
    document.getElementById("loginUsername").value = "ahmet";
    document.getElementById("loginPassword").value = "secret";

    document.getElementById("loginForm").dispatchEvent(new Event("submit", { cancelable: true }));

    await vi.waitFor(() => expect(JSON.parse(localStorage.getItem("alkaros_waiter_offline_budget"))).toMatchObject({ budgetId: "b1", userId: "u1", roleCode: "waiter" }));
  });

  it.each([
    ["the browser comes back online", () => window.dispatchEvent(new Event("online"))],
    ["the app becomes visible again", () => {
      Object.defineProperty(document, "visibilityState", { value: "visible", configurable: true });
      document.dispatchEvent(new Event("visibilitychange"));
    }],
  ])("sends the queued offline actions once when %s", async (_name, trigger) => {
    withQueuedAction();
    const fetchMock = await startSignedIn();
    expect(reconcileCalls(fetchMock)).toHaveLength(0);

    trigger();

    await vi.waitFor(() => expect(reconcileCalls(fetchMock)).toHaveLength(1));
    expect(JSON.parse(reconcileCalls(fetchMock)[0][1].body).actions.map((action) => action.idempotencyKey)).toEqual(["k-1"]);
    await vi.waitFor(() => expect(JSON.parse(localStorage.getItem("alkaros_waiter_offline_actions"))).toEqual([]));
  });

  it("does not reconcile when the app goes to the background", async () => {
    withQueuedAction();
    const fetchMock = await startSignedIn();

    Object.defineProperty(document, "visibilityState", { value: "hidden", configurable: true });
    document.dispatchEvent(new Event("visibilitychange"));
    await new Promise((resolve) => setTimeout(resolve, 50));

    expect(reconcileCalls(fetchMock)).toHaveLength(0);
  });

  it("sends the queued offline actions when the live connection to the server is up", async () => {
    const hub = { start: () => Promise.resolve(), on() {}, onreconnecting() {}, onreconnected() {}, onclose() {}, stop: () => Promise.resolve() };
    const builder = { withUrl: () => builder, withAutomaticReconnect: () => builder, configureLogging: () => builder, build: () => hub };
    globalThis.signalR = { HubConnectionBuilder: function () { return builder; }, LogLevel: { Warning: 3 } };
    withQueuedAction();

    const fetchMock = await startSignedIn();

    await vi.waitFor(() => expect(reconcileCalls(fetchMock)).toHaveLength(1));
  });
});

// A modifier group whose ceiling is 0 allows no extras (the Host refuses them), so the picker must not offer them.
describe("product sheet modifier ceiling", () => {
  it("refuses an extra from a group that allows none", async () => {
    document.body.innerHTML = `<div id="toasts"></div><div id="ribbon"></div><span id="ribbonText"></span><button id="ribbonQueue"></button>
<div id="optionsBackdrop"></div><div id="optionsSheet"></div><span id="optionsTitle"></span><span id="optionsSub"></span><div id="optionsBody"></div>
<button id="optionsClose"></button><button id="optionsConfirm"></button><span id="optionsFootLabel"></span><span id="optionsFootValue"></span>`;
    const wwwroot = resolve(repoRoot, "src/Clients/WaiterPwa/wwwroot/js");
    const { state, el } = await import(resolve(wwwroot, "state.js"));
    const { toggleModifier } = await import(resolve(wwwroot, "sheets/product-sheet.js"));
    state.optionsContext = {
      product: {
        modifierGroups: [{ modifierGroupId: "g1", name: "Sos", maxSelections: 0, modifiers: [{ modifierId: "m1", name: "Acı", priceDelta: 0 }] }],
      },
      quantity: 1,
      chosen: new Map(),
    };

    toggleModifier({ dataset: { modifier: "m1", group: "g1" } });

    expect(state.optionsContext.chosen.size).toBe(0);
    expect(el.toasts.textContent).toContain("Sos için seçim yapılamaz.");
  });
});
