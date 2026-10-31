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
        on: () => undefined,
        onreconnecting: () => undefined,
        onreconnected: () => undefined,
        onclose: () => undefined,
        start: vi.fn(async () => undefined),
        stop: vi.fn(async () => undefined),
      };
    }
  },
}));

const sizeGroup = {
  modifierGroupId: "g1",
  code: "BOY",
  name: "Porsiyon boyu",
  selectionType: "Single",
  minSelections: 1,
  maxSelections: 1,
  modifiers: [
    { modifierId: "m-big", code: "BIG", name: "Büyük boy", priceDelta: 5 },
    { modifierId: "m-small", code: "SMALL", name: "Küçük boy", priceDelta: 0 },
  ],
};

let root: Root | null = null;

afterEach(() => {
  if (root) {
    act(() => root!.unmount());
    root = null;
  }
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function openCashier() {
  const added: unknown[] = [];
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
    if (path.endsWith("/catalog")) {
      return jsonResponse([
        { productId: "p-pide", sku: "SKU-1", name: "Pide", categoryCode: "ANA", categoryName: "Ana Yemek", unitPrice: 100, taxRate: 10, modifierGroups: [sizeGroup] },
        { productId: "p-tost", sku: "SKU-3", name: "Tost", categoryCode: "ANA", categoryName: "Ana Yemek", unitPrice: 50, taxRate: 10, modifierGroups: [{ ...sizeGroup, modifierGroupId: "g2", name: "Sos", minSelections: 0 }] },
        { productId: "p-ayran", sku: "SKU-2", name: "Ayran", categoryCode: "ICE", categoryName: "İçecek", unitPrice: 20, taxRate: 10 },
      ]);
    }
    if (path.endsWith("/orders/active")) {
      return jsonResponse({
        displayId: "d", terminalId: "terminal", orderId: "order-1", revision: 3, state: "Active", editable: true,
        orderNumber: "1", lines: [], subtotal: 0, discountTotal: 0, taxTotal: 0, total: 0, currency: "TRY", serverTimestamp: "", message: "",
      });
    }
    if (path.endsWith("/orders/order-1/items") && init?.method === "POST") {
      added.push(JSON.parse(String(init.body)));
      return jsonResponse({ orderId: "order-1", revision: 4 });
    }
    if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
    return jsonResponse({});
  }));

  const { Cashier } = await import("./Cashier");
  const { RouterProvider } = await import("../router");
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  await act(async () => {
    root!.render(createElement(RouterProvider, null, createElement(Cashier)));
  });
  await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  return added;
}

const click = (element: Element) => act(async () => {
  element.dispatchEvent(new MouseEvent("click", { bubbles: true }));
  await new Promise((resolve) => setTimeout(resolve, 0));
});

const card = (name: string) =>
  [...document.querySelectorAll<HTMLButtonElement>(".product-card")].find((button) => button.textContent?.includes(name))!;

const button = (label: string) =>
  [...document.querySelectorAll<HTMLButtonElement>("button")].find((candidate) => candidate.textContent === label)!;

describe("Cashier asks for a product's options before selling it", () => {
  it("adds a product without option groups straight away and without options", async () => {
    const added = await openCashier();

    await click(card("Ayran"));

    expect(document.querySelector('[role="dialog"]')).toBeNull();
    expect(added).toEqual([{ productId: "p-ayran", quantity: 1, expectedRevision: 3 }]);
  });

  it("opens the picker for a product with a required group and sends the choice with the line", async () => {
    const added = await openCashier();

    await click(card("Pide"));

    expect(document.querySelector('[role="dialog"]')?.textContent).toContain("Porsiyon boyu (zorunlu)");
    expect(added).toEqual([]);
    expect(button("Siparişe ekle").disabled).toBe(true);

    await click(document.querySelector<HTMLInputElement>('input[type="radio"]')!);
    expect(button("Siparişe ekle").disabled).toBe(false);
    await click(button("Siparişe ekle"));

    expect(document.querySelector('[role="dialog"]')).toBeNull();
    expect(added).toEqual([{ productId: "p-pide", quantity: 1, expectedRevision: 3, modifiers: [{ modifierId: "m-big" }] }]);
  });

  it("lets the cashier take back the choice of an optional single-choice group", async () => {
    const added = await openCashier();

    await click(card("Tost"));
    const option = document.querySelector<HTMLInputElement>('input[type="checkbox"]')!;
    await click(option);
    expect(option.checked).toBe(true);
    await click(option);
    expect(option.checked).toBe(false);
    await click(button("Siparişe ekle"));

    expect(added).toEqual([{ productId: "p-tost", quantity: 1, expectedRevision: 3 }]);
  });

  it("sells nothing when the picker is cancelled", async () => {
    const added = await openCashier();

    await click(card("Pide"));
    await click(button("Vazgeç"));

    expect(document.querySelector('[role="dialog"]')).toBeNull();
    expect(added).toEqual([]);
  });
});
