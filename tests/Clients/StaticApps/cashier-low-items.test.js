import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const wwwroot = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot");
const appHtml = resolve(wwwroot, "index.html");
const appScript = resolve(wwwroot, "cashier-app.js");

const TERMINAL = "11111111-1111-1111-1111-111111111111";
const PRODUCT = "22222222-2222-2222-2222-222222222222";
const GONE = "55555555-5555-5555-5555-555555555555";
const ORDER = "33333333-3333-3333-3333-333333333333";

const catalogOf = (price) => [{ productId: PRODUCT, name: "Kola", unitPrice: price, categoryCode: "c1", categoryName: "İçecek", sku: "K1" }];

function routes({ userId = "u1", price = 45, draft = { status: 200, body: { orderId: ORDER, rowVersion: 1 } } } = {}) {
  return [
    { test: (url) => url.endsWith("/auth/session/current"), body: { terminalId: TERMINAL, userId, displayName: "Test Kasiyer" } },
    { test: (url) => url.endsWith("/catalog"), body: catalogOf(price) },
    { test: (url) => url.endsWith("/orders/staff"), body: [] },
    { test: (url) => url.endsWith("/orders/suggested-waiter"), status: 404 },
    { test: (url) => url.endsWith("/orders/waiter-load"), body: [] },
    { test: (url, init) => url.endsWith("/orders/table-draft") && init?.method === "POST", ...draft },
    { test: (url) => url.endsWith("/submit-draft"), body: { orderId: ORDER, rowVersion: 2 } },
    { test: (url) => url.endsWith("/send-to-cashier"), status: 204 },
  ];
}

async function openTill(options) {
  const fetchMock = installFetchRouter(routes(options));
  await loadApp(appHtml, appScript);
  await vi.waitFor(() => expect(document.querySelector(".pos-product-card")).not.toBeNull());
  return fetchMock;
}

const toasts = () => [...document.querySelectorAll("#toastRegion .toast")];
const draftBodies = (fetchMock) =>
  fetchMock.mock.calls.filter(([url]) => String(url).endsWith("/orders/table-draft")).map(([, init]) => JSON.parse(init.body));

describe("cashier-app.js low items", () => {
  beforeEach(() => {
    vi.resetModules();
    localStorage.clear();
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
    localStorage.clear();
    document.body.innerHTML = "";
  });

  it("keeps an error notice until the cashier closes it, while a success notice goes away by itself", async () => {
    const fetchMock = await openTill({ draft: { status: 500, body: {} } });
    document.querySelector(".pos-product-card").click();
    vi.useFakeTimers({ toFake: ["setTimeout"] });

    document.getElementById("btnDispatchOrder").click();
    await vi.waitFor(() => expect(toasts().length).toBe(1));
    vi.advanceTimersByTime(60_000);

    expect(toasts()).toHaveLength(1);
    const close = toasts()[0].querySelector("button");
    expect(close.textContent).toBe("Kapat");
    close.click();
    expect(toasts()).toHaveLength(0);
    expect(fetchMock).toHaveBeenCalled();
  });

  it("removes a success notice after a few seconds", async () => {
    await openTill();
    document.querySelector(".pos-product-card").click();
    vi.useFakeTimers({ toFake: ["setTimeout"] });

    document.getElementById("btnDispatchOrder").click();
    await vi.waitFor(() => expect(toasts().some((toast) => toast.textContent.includes("mutfağa iletildi"))).toBe(true));
    vi.advanceTimersByTime(6_000);

    expect(toasts()).toHaveLength(0);
  });

  it("resends the same round after a reload instead of opening a second order, and forgets it once it went through", async () => {
    const first = await openTill({ draft: { status: 500, body: {} } });
    document.querySelector(".pos-product-card").click();
    document.getElementById("btnDispatchOrder").click();
    await vi.waitFor(() => expect(draftBodies(first)).toHaveLength(1));
    await vi.waitFor(() => expect(toasts()).toHaveLength(1));
    const sentBefore = draftBodies(first)[0];

    document.body.innerHTML = "";
    vi.resetModules();
    const second = await openTill();
    document.getElementById("btnDispatchOrder").click();
    await vi.waitFor(() => expect(draftBodies(second)).toHaveLength(1));

    expect(draftBodies(second)[0].id).toBe(sentBefore.id);
    await vi.waitFor(() => expect(localStorage.getItem("alkaros_cashier_pending_dispatch")).toBeNull());
  });

  it("does not resend a saved round when the cart no longer holds exactly its lines", async () => {
    const first = await openTill({ draft: { status: 500, body: {} } });
    document.querySelector(".pos-product-card").click();
    document.getElementById("btnDispatchOrder").click();
    await vi.waitFor(() => expect(draftBodies(first)).toHaveLength(1));
    const sentBefore = draftBodies(first)[0];

    document.body.innerHTML = "";
    vi.resetModules();
    const second = await openTill();
    document.querySelector(".pos-product-card").click();
    document.getElementById("btnDispatchOrder").click();
    await vi.waitFor(() => expect(draftBodies(second)).toHaveLength(1));

    expect(draftBodies(second)[0].id).not.toBe(sentBefore.id);
    expect(draftBodies(second)[0].items.map((line) => line.quantity)).toEqual([2]);
  });

  it("does not hand one cashier's unanswered round to the next cashier", async () => {
    const first = await openTill({ draft: { status: 500, body: {} } });
    document.querySelector(".pos-product-card").click();
    document.getElementById("btnDispatchOrder").click();
    await vi.waitFor(() => expect(draftBodies(first)).toHaveLength(1));
    const sentBefore = draftBodies(first)[0];

    document.body.innerHTML = "";
    vi.resetModules();
    const second = await openTill({ userId: "other-cashier" });
    document.getElementById("btnDispatchOrder").click();
    await vi.waitFor(() => expect(draftBodies(second)).toHaveLength(1));

    expect(draftBodies(second)[0].id).not.toBe(sentBefore.id);
  });

  it("shows one copy of a repeated error, with a close button that names what it closes", async () => {
    await openTill({ draft: { status: 500, body: {} } });
    document.querySelector(".pos-product-card").click();
    for (let press = 0; press < 3; press++) {
      document.getElementById("btnDispatchOrder").click();
      await vi.waitFor(() => expect(document.getElementById("btnDispatchOrder").disabled).toBe(false));
    }
    expect(toasts()).toHaveLength(1);
    expect(toasts()[0].querySelector("button").getAttribute("aria-label")).toBe("Bildirimi kapat");
  });

  it("brings a restored cart to today's prices and drops a product that is no longer sold", async () => {
    localStorage.setItem("alkaros_cashier_active_ticket", JSON.stringify([
      { id: "l1", productId: PRODUCT, name: "Kola", price: 10, quantity: 2, note: "", modifiers: [] },
      { id: "l2", productId: GONE, name: "Eski ürün", price: 7, quantity: 1, note: "", modifiers: [] },
    ]));

    await openTill({ price: 45 });

    await vi.waitFor(() => expect(document.getElementById("ticketItemsStream").textContent).not.toContain("Eski ürün"));
    const saved = JSON.parse(localStorage.getItem("alkaros_cashier_active_ticket"));
    expect(saved.map((line) => [line.productId, line.price])).toEqual([[PRODUCT, 45]]);
    expect(toasts().some((toast) => toast.textContent.includes("artık satışta değil"))).toBe(true);
  });
});

describe("split-payment.js without a bill", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("tells the cashier what to do in plain words and offers the way back to the pending checks", async () => {
    const dir = resolve(wwwroot, "payments/split-payment");
    installFetchRouter([{ test: (url) => url.endsWith("/auth/session/current"), body: { terminalId: TERMINAL, userId: "u1" } }]);
    window.history.replaceState({}, "", "/");
    await loadApp(resolve(dir, "index.html"), resolve(dir, "split-payment.js"));
    await vi.waitFor(() => expect(document.querySelector(".sp-title")?.textContent).toBe("Hesap Bulunamadı"));

    expect(document.body.textContent).not.toContain("billId");
    expect(document.body.textContent).toContain("Hesap açılamadı. Bekleyen hesaplardan tekrar seçin.");
    const back = document.querySelector("a.sp-btn");
    expect(back.textContent).toBe("Bekleyen hesaplara dön");
    expect(back.getAttribute("href")).toBe("/pending-checks");
  });
});
