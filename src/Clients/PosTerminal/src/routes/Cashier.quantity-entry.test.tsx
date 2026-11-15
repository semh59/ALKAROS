// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { "content-type": "application/json" } });

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

const line = { lineId: "l", itemId: "i1", productId: "p-ayran", name: "Ayran", quantity: 1, unitPrice: 20, lineTotal: 20, discountAmount: 0 };
const order = (editable = true) => ({
  displayId: "d", terminalId: "terminal", orderId: "order-1", revision: 3, state: "Active", editable, orderNumber: "1",
  lines: [line], subtotal: 20, discountTotal: 0, taxTotal: 0, total: 20, currency: "TRY", serverTimestamp: "", message: "",
});

let root: Root | null = null;
afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function openCashier(editable = true) {
  const patches: Array<{ quantity: number; expectedRevision: number }> = [];
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
    if (path.endsWith("/catalog")) return jsonResponse([{ productId: "p-ayran", sku: "AYR-01", name: "Ayran", categoryCode: "ICE", categoryName: "İçecek", unitPrice: 20, taxRate: 10 }]);
    if (path.endsWith("/orders/active")) return jsonResponse(order(editable));
    if (path.endsWith("/orders/order-1/items/i1") && init?.method === "PATCH") {
      patches.push(JSON.parse(String(init.body)));
      return jsonResponse({ orderId: "order-1", revision: 4 });
    }
    if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
    return jsonResponse({});
  }));
  const { Cashier } = await import("./Cashier");
  const { RouterProvider } = await import("../router");
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  await act(async () => { root!.render(createElement(RouterProvider, null, createElement(Cashier))); });
  for (let i = 0; i < 3; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  return patches;
}

const quantityButton = () => document.querySelector<HTMLButtonElement>('button[aria-label="Ayran adedini değiştir"]');
const quantityInput = () => document.querySelector<HTMLInputElement>('input[aria-label="Ayran adedi"]');
const settle = () => act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
const openEntry = async () => {
  await act(async () => { quantityButton()!.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
};
const typeQuantity = async (text: string) => {
  await act(async () => {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(quantityInput(), text);
    quantityInput()!.dispatchEvent(new Event("input", { bubbles: true }));
  });
};
const press = async (key: string) => {
  await act(async () => { quantityInput()!.dispatchEvent(new KeyboardEvent("keydown", { key, bubbles: true, cancelable: true })); });
  await settle();
};
const leave = async () => {
  await act(async () => { quantityInput()?.dispatchEvent(new FocusEvent("focusout", { bubbles: true })); });
  await settle();
};

describe("Cashier line quantity can be typed", () => {
  it("opens a number box with the current quantity and sets the typed quantity on Enter", async () => {
    const patches = await openCashier();
    await openEntry();

    expect(quantityInput()!.value).toBe("1");
    expect(document.activeElement).toBe(quantityInput());
    await typeQuantity("10");
    await press("Enter");

    expect(patches).toEqual([{ quantity: 10, expectedRevision: 3 }]);
    expect(quantityInput()).toBeNull();
  });

  it("sets the typed quantity when the box is left", async () => {
    const patches = await openCashier();
    await openEntry();
    await typeQuantity("4");
    await leave();

    expect(patches).toEqual([{ quantity: 4, expectedRevision: 3 }]);
  });

  it("sends once when Enter is followed by leaving the box", async () => {
    const patches = await openCashier();
    await openEntry();
    await typeQuantity("6");
    const box = quantityInput()!;
    await act(async () => {
      box.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", bubbles: true, cancelable: true }));
      box.dispatchEvent(new FocusEvent("focusout", { bubbles: true }));
    });
    await settle();

    expect(patches).toHaveLength(1);
  });

  it("Escape cancels and a later blur does not send", async () => {
    const patches = await openCashier();
    await openEntry();
    await typeQuantity("9");
    const box = quantityInput()!;
    await act(async () => {
      box.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true, cancelable: true }));
      box.dispatchEvent(new FocusEvent("focusout", { bubbles: true }));
    });
    await settle();

    expect(patches).toEqual([]);
    expect(quantityInput()).toBeNull();
  });

  it.each(["0", "-2", "abc", "", "1"])("ignores %j", async (text) => {
    const patches = await openCashier();
    await openEntry();
    await typeQuantity(text);
    await press("Enter");

    expect(patches).toEqual([]);
    expect(quantityInput()).toBeNull();
  });

  it("accepts a comma as the decimal mark", async () => {
    const patches = await openCashier();
    await openEntry();
    await typeQuantity("2,5");
    await press("Enter");

    expect(patches).toEqual([{ quantity: 2.5, expectedRevision: 3 }]);
  });

  it("cannot be opened on an order that was already sent", async () => {
    await openCashier(false);

    expect(quantityButton()?.disabled ?? true).toBe(true);
  });
});
