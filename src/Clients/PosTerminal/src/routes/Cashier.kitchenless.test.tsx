// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { markTableCart } from "../tableCart";
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
const sentOrder = {
  displayId: "d", terminalId: "terminal", orderId: "order-1", revision: 5, state: "Active", editable: false, orderNumber: "1",
  lines: [], subtotal: 0, discountTotal: 0, taxTotal: 0, total: 0, currency: "TRY", serverTimestamp: "", message: "",
};

const mixedLine = (n: number, unsent: boolean) => ({ itemId: `i-${n}`, name: `Ürün ${n}`, quantity: 1, unitPrice: 20, lineTotal: 20, discountAmount: 0, ...(unsent ? { unsent } : {}) });

let root: Root | null = null;
afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function openTill(capabilities: string[], order: Record<string, unknown> = sentOrder) {
  const calls: { call: string; body?: Record<string, unknown> }[] = [];
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    const method = init?.method ?? "GET";
    calls.push({
      call: `${method} ${path.replace(/^.*\/api\/v1\/terminals\/[^/]+/, "")}`,
      body: init?.body ? JSON.parse(String(init.body)) : undefined,
    });
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities });
    if (path.endsWith("/catalog")) return jsonResponse(catalog);
    if (path.endsWith("/orders/active")) return jsonResponse(order);
    if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
    if (path.endsWith("/items/without-kitchen")) return jsonResponse({ orderId: "order-1", itemId: "i-1", rowVersion: 6, reasonCode: "waiter-forgot" });
    return jsonResponse({});
  }));
  const { Cashier } = await import("./Cashier");
  const { RouterProvider } = await import("../router");
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  await act(async () => { root!.render(createElement(RouterProvider, null, createElement(Cashier))); });
  for (let i = 0; i < 3; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  return calls;
}

const settle = () => act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
const tap = (element: Element) => act(async () => { element.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
const button = (label: string) => [...document.querySelectorAll<HTMLButtonElement>("button")].find((b) => b.textContent?.includes(label));
const card = (name: string) => [...document.querySelectorAll<HTMLButtonElement>(".product-card")].find((b) => b.textContent?.includes(name))!;

describe("Cashier adds an item to the open check without a kitchen ticket", () => {
  it("shows the button only to a cashier who holds the permission", async () => {
    await openTill(["orders.create"]);
    expect(button("Mutfağa göndermeden hesaba ekle")).toBeUndefined();
  });

  it("asks for a reason, then sends the tapped product to the kitchen-less endpoint of the open order", async () => {
    const calls = await openTill(["orders.create", "orders.add-without-kitchen"]);

    await tap(button("Mutfağa göndermeden hesaba ekle")!);
    expect(button("Garson unuttu")).toBeDefined();
    expect(button("Müşteri istedi")).toBeDefined();
    expect(button("Diğer")).toBeDefined();
    await tap(button("Garson unuttu")!);
    expect(document.body.textContent).toContain("Dokunduğunuz ürünler mutfağa gönderilmeden eklenir. Neden: Garson unuttu");
    await tap(card("Ayran"));
    await settle();

    const added = calls.find((c) => c.call === "POST /orders/order-1/items/without-kitchen");
    expect(added?.body).toMatchObject({ productId: "p-ayran", quantity: 1, expectedRowVersion: 5, reasonCode: "waiter-forgot" });
    expect(typeof added?.body?.idempotencyKey).toBe("string");
    expect(calls.some((c) => c.call === "POST /orders" || c.call.endsWith("/items"))).toBe(false);
  });

  it("does not take a product with options on this path and says so", async () => {
    const calls = await openTill(["orders.create", "orders.add-without-kitchen"]);
    await tap(button("Mutfağa göndermeden hesaba ekle")!);
    await tap(button("Müşteri istedi")!);

    await tap(card("Pide"));
    await settle();

    expect(calls.some((c) => c.call.endsWith("/without-kitchen"))).toBe(false);
    expect(document.body.textContent).toContain("Seçenekli ürün mutfağa göndermeden eklenemez");
  });

  it("leaves the kitchen-less mode with the done button", async () => {
    await openTill(["orders.create", "orders.add-without-kitchen"]);
    await tap(button("Mutfağa göndermeden hesaba ekle")!);
    await tap([...document.querySelectorAll<HTMLButtonElement>("button")].find((b) => b.textContent === "Diğer")!);

    await tap(button("Normal eklemeye dön")!);

    expect(document.body.textContent).not.toContain("Dokunduğunuz ürünler mutfağa gönderilmeden");
  });
  it("closes the reason picker without entering the mode", async () => {
    await openTill(["orders.create", "orders.add-without-kitchen"]);
    await tap(button("Mutfağa göndermeden hesaba ekle")!);

    await tap(button("Vazgeç")!);

    expect(button("Garson unuttu")).toBeUndefined();
    expect(button("Mutfağa göndermeden hesaba ekle")).toBeDefined();
  });
  it("hides the button while the order is still an unsent draft", async () => {
    await openTill(["orders.create", "orders.add-without-kitchen"], { ...sentOrder, editable: true, lines: [mixedLine(1, true), mixedLine(2, true)] });

    expect(button("Mutfağa göndermeden hesaba ekle")).toBeUndefined();
  });

  it("keeps the button when an unsent cart sits on a table order that already has sent lines", async () => {
    await openTill(["orders.create", "orders.add-without-kitchen"], { ...sentOrder, editable: true, lines: [mixedLine(1, false), mixedLine(2, true)] });

    expect(button("Mutfağa göndermeden hesaba ekle")).toBeDefined();
  });

  it("sends a tapped product to the kitchen-less endpoint while an unsent cart is present", async () => {
    const calls = await openTill(["orders.create", "orders.add-without-kitchen"], { ...sentOrder, editable: true, lines: [mixedLine(1, false), mixedLine(2, true)] });
    await tap(button("Mutfağa göndermeden hesaba ekle")!);
    await tap(button("Garson unuttu")!);

    await tap(card("Ayran"));
    await settle();

    expect(calls.some((c) => c.call === "POST /orders/order-1/items/without-kitchen")).toBe(true);
    expect(calls.some((c) => c.call.endsWith("/items"))).toBe(false);
  });

  it("leaves the kitchen-less mode once the unsent cart is sent to the kitchen", async () => {
    const order = { ...sentOrder, editable: true, lines: [mixedLine(1, false), mixedLine(2, true)] };
    await openTill(["orders.create", "orders.add-without-kitchen"], order);
    await tap(button("Mutfağa göndermeden hesaba ekle")!);
    await tap(button("Garson unuttu")!);
    expect(document.body.textContent).toContain("Dokunduğunuz ürünler mutfağa gönderilmeden");

    order.editable = false;
    await tap(button("Mutfağa gönder (1)")!);
    await settle();

    expect(document.body.textContent).not.toContain("Dokunduğunuz ürünler mutfağa gönderilmeden");
  });

  it("opens the picker for the table panel's request although an unsent cart is present", async () => {
    markTableCart("order-1", true);
    await openTill(["orders.create", "orders.add-without-kitchen"], { ...sentOrder, editable: true, lines: [mixedLine(1, false), mixedLine(2, true)] });

    expect(button("Garson unuttu")).toBeDefined();
  });

  it("opens the reason picker straight away when the table panel asked for the kitchen-less path", async () => {
    markTableCart("order-1", true);
    await openTill(["orders.create", "orders.add-without-kitchen"]);

    expect(button("Garson unuttu")).toBeDefined();
  });

  it("does not open the picker on a plain entry, even after an earlier kitchen-less entry that was never used", async () => {
    markTableCart("order-1", true);
    markTableCart("order-1");
    await openTill(["orders.create", "orders.add-without-kitchen"]);

    expect(button("Garson unuttu")).toBeUndefined();
  });

  it("counts only the unsent lines on the send button", async () => {
    await openTill(["orders.create"], { ...sentOrder, editable: true, lines: [mixedLine(1, false), mixedLine(2, false), mixedLine(3, true)] });

    expect(button("Mutfağa gönder (1)")).toBeDefined();
  });
});
