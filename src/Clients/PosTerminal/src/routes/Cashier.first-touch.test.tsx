// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });

vi.mock("@microsoft/signalr", () => ({
  LogLevel: { Warning: 2 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      return {
        on: () => undefined, onreconnecting: () => undefined, onreconnected: () => undefined, onclose: () => undefined,
        start: vi.fn(async () => undefined), stop: vi.fn(async () => undefined),
      };
    }
  },
}));

const sizeGroup = {
  modifierGroupId: "g1", code: "BOY", name: "Porsiyon boyu", selectionType: "Single", minSelections: 1, maxSelections: 1,
  modifiers: [{ modifierId: "m-big", code: "BIG", name: "Büyük boy", priceDelta: 5 }],
};
const catalog = [
  { productId: "p-pide", sku: "SKU-1", name: "Pide", categoryCode: "ANA", categoryName: "Ana Yemek", unitPrice: 100, taxRate: 10, modifierGroups: [sizeGroup] },
  { productId: "p-ayran", sku: "SKU-2", name: "Ayran", categoryCode: "ICE", categoryName: "İçecek", unitPrice: 20, taxRate: 10 },
];
const snapshot = (orderId: string, extra: Record<string, unknown> = {}) => ({
  displayId: "d", terminalId: "terminal", orderId, revision: 1, state: "Active", editable: true, orderNumber: "1",
  lines: [], subtotal: 0, discountTotal: 0, taxTotal: 0, total: 0, currency: "TRY", serverTimestamp: "", message: "", ...extra,
});

let root: Root | null = null;
afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

interface Till { calls: string[]; bodies: Record<string, unknown>[]; gates: Promise<void>[]; setActive: (order: ReturnType<typeof snapshot> | null) => void; }

/**
 * `active` is what GET orders/active answers (null: no order). `startOrder` decides the answer of POST orders;
 * `addItem` that of POST orders/{id}/items.
 */
async function openTill({
  active = null as ReturnType<typeof snapshot> | null,
  startOrder = async () => jsonResponse({ orderId: "order-new", orderNumber: "7", revision: 1 }),
  addItem = async () => jsonResponse({ orderId: "order-new", revision: 2 }),
} = {}): Promise<Till> {
  let current = active;
  const till: Till = { calls: [], bodies: [], gates: [], setActive: (order) => { current = order; } };
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    const method = init?.method ?? "GET";
    till.calls.push(`${method} ${path.replace(/^.*\/api\/v1\/terminals\/[^/]+/, "")}`);
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities: [] });
    if (path.endsWith("/catalog")) return jsonResponse(catalog);
    if (path.endsWith("/orders/active")) {
      const answer = current;
      const gate = till.gates.shift();
      if (gate) await gate;
      return jsonResponse(answer);
    }
    if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
    if (path.endsWith("/orders") && method === "POST") {
      const answer = await startOrder();
      if (answer.ok) current = snapshot("order-new");
      return answer;
    }
    if (path.endsWith("/items") && method === "POST") {
      till.bodies.push(JSON.parse(String(init!.body)));
      return addItem();
    }
    return jsonResponse({});
  }));
  const { Cashier } = await import("./Cashier");
  const { RouterProvider } = await import("../router");
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  await act(async () => { root!.render(createElement(RouterProvider, null, createElement(Cashier))); });
  for (let i = 0; i < 3; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  return till;
}

const settle = () => act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
const tap = (element: Element) => act(async () => { element.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
const card = (name: string) => [...document.querySelectorAll<HTMLButtonElement>(".product-card")].find((b) => b.textContent?.includes(name))!;
const buttonLabels = () => [...document.querySelectorAll("button")].map((b) => b.textContent);
const posts = (till: Till, suffix: string) => till.calls.filter((call) => call.startsWith("POST") && call.endsWith(suffix));

describe("Cashier opens the order on the first product tap", () => {
  it("shows live product cards and no open-order button while no order exists", async () => {
    await openTill();

    expect(card("Ayran").disabled).toBe(false);
    for (const label of ["Yeni sipariş aç", "Sipariş aç", "Sonraki siparişi aç", "Sonraki sipariş"]) expect(buttonLabels()).not.toContain(label);
    expect(document.body.textContent).toContain("Ürüne dokununca sipariş açılır");
  });

  it("opens an order and adds the first product with the revision the opening answered", async () => {
    const till = await openTill();

    await tap(card("Ayran"));
    await settle();

    expect(posts(till, "/orders")).toHaveLength(1);
    expect(till.calls.indexOf("POST /orders")).toBeLessThan(till.calls.indexOf("POST /orders/order-new/items"));
    expect(till.bodies).toEqual([{ productId: "p-ayran", quantity: 1, expectedRevision: 1 }]);
  });

  it("a second tap while the first is still opening does not open a second order", async () => {
    let release: () => void = () => undefined;
    const till = await openTill({
      startOrder: () => new Promise((resolve) => { release = () => resolve(jsonResponse({ orderId: "order-new", orderNumber: "7", revision: 1 })); }),
    });

    // Three taps inside one frame: React has not drawn the busy state yet, only the in-flight guard can stop them.
    await act(async () => {
      for (let i = 0; i < 3; i++) card("Ayran").dispatchEvent(new MouseEvent("click", { bubbles: true }));
    });
    release();
    await settle();
    await settle();

    expect(posts(till, "/orders")).toHaveLength(1);
    expect(till.bodies).toHaveLength(1);
  });

  it("a product with required options opens the order only after the choice is confirmed, never when it is cancelled", async () => {
    const till = await openTill();

    await tap(card("Pide"));
    await settle();
    expect(document.querySelector('[role="dialog"]')).not.toBeNull();
    expect(posts(till, "/orders")).toHaveLength(0);

    await tap([...document.querySelectorAll("button")].find((b) => b.textContent === "Vazgeç")!);
    await settle();
    expect(posts(till, "/orders")).toHaveLength(0);

    await tap(card("Pide"));
    await tap(document.querySelector<HTMLInputElement>('input[type="radio"]')!);
    await tap([...document.querySelectorAll("button")].find((b) => b.textContent === "Siparişe ekle")!);
    await settle();

    expect(posts(till, "/orders")).toHaveLength(1);
    expect(till.bodies).toEqual([{ productId: "p-pide", quantity: 1, expectedRevision: 1, modifiers: [{ modifierId: "m-big" }] }]);
  });

  it("when adding the first product fails, the opened empty order stays on screen and the next tap uses it", async () => {
    let failNext = true;
    const till = await openTill({
      addItem: async () => {
        if (failNext) { failNext = false; return jsonResponse({ error: { code: "X", message: "sunucu metni" } }, 500); }
        return jsonResponse({ orderId: "order-new", revision: 2 });
      },
    });

    await tap(card("Ayran"));
    await settle();
    await settle();

    expect(document.body.textContent).toContain("Soldaki katalogdan ürün seçin.");

    await tap(card("Ayran"));
    await settle();

    expect(posts(till, "/orders")).toHaveLength(1);
    expect(till.bodies).toHaveLength(2);
  });

  it("a submitted order is followed by a new one on the next product tap", async () => {
    const till = await openTill({ active: snapshot("order-old", { editable: false }) });

    await tap(card("Ayran"));
    await settle();

    expect(posts(till, "/orders")).toHaveLength(1);
    expect(till.calls).not.toContain("POST /orders/order-old/items");
  });

  it("an open editable order is reused, no second order is opened", async () => {
    const till = await openTill({ active: snapshot("order-open", { revision: 4 }) });

    await tap(card("Ayran"));
    await settle();

    expect(posts(till, "/orders")).toHaveLength(0);
    expect(till.bodies).toEqual([{ productId: "p-ayran", quantity: 1, expectedRevision: 4 }]);
  });

  it("an order opened while the cashier was on another screen (a table order) is the one the first tap fills", async () => {
    const till = await openTill();
    till.setActive(snapshot("order-table", { revision: 3 }));

    await act(async () => { window.history.pushState(null, "", "/pending-checks"); window.dispatchEvent(new PopStateEvent("popstate")); });
    await settle();
    await act(async () => { window.history.pushState(null, "", "/"); window.dispatchEvent(new PopStateEvent("popstate")); });
    await settle();
    await tap(card("Ayran"));
    await settle();

    expect(posts(till, "/orders")).toHaveLength(0);
    expect(till.bodies).toEqual([{ productId: "p-ayran", quantity: 1, expectedRevision: 3 }]);
  });

  it("when opening an order is refused because the till already holds an editable one, the tap continues with that order", async () => {
    const till = await openTill({ startOrder: async () => jsonResponse({ error: { code: "ORDER_START_REFUSED", message: "x" } }, 409) });
    till.setActive(snapshot("order-held", { revision: 5 }));

    await tap(card("Ayran"));
    await settle();

    expect(till.bodies).toEqual([{ productId: "p-ayran", quantity: 1, expectedRevision: 5 }]);
    expect(till.calls).toContain("POST /orders/order-held/items");
  });

  it("a slow, older read of the till's order never replaces a newer one", async () => {
    const till = await openTill();
    let release: () => void = () => undefined;
    till.gates.push(new Promise<void>((resolve) => { release = resolve; }));
    const go = async (path: string) => act(async () => { window.history.pushState(null, "", path); window.dispatchEvent(new PopStateEvent("popstate")); });

    await go("/pending-checks");
    await go("/"); // this read answers "no order" but is held back
    till.setActive(snapshot("order-table", { revision: 3 }));
    await go("/pending-checks");
    await go("/"); // this read answers the table order at once
    await settle();
    release();
    await settle();
    await tap(card("Ayran"));
    await settle();

    expect(posts(till, "/orders")).toHaveLength(0);
    expect(till.bodies).toEqual([{ productId: "p-ayran", quantity: 1, expectedRevision: 3 }]);
  });
});
