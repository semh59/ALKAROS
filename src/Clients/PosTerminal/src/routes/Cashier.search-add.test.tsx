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

const sizeGroup = {
  modifierGroupId: "g1", code: "BOY", name: "Porsiyon boyu", selectionType: "Single", minSelections: 1, maxSelections: 1,
  modifiers: [{ modifierId: "m-big", code: "BIG", name: "Büyük boy", priceDelta: 5 }],
};
const catalog = [
  { productId: "p-pide", sku: "PİDE-1", name: "Pide", categoryCode: "ANA", categoryName: "Ana Yemek", unitPrice: 100, taxRate: 10, modifierGroups: [sizeGroup] },
  { productId: "p-ayran", sku: "AYR-01", name: "Ayran", categoryCode: "ICE", categoryName: "İçecek", unitPrice: 20, taxRate: 10 },
  { productId: "p-borek", sku: "SIM-011", name: "Börek", categoryCode: "ANA", categoryName: "Ana Yemek", unitPrice: 30, taxRate: 10 },
  { productId: "p-simit", sku: "SIM-01", name: "Simit", categoryCode: "ANA", categoryName: "Ana Yemek", unitPrice: 10, taxRate: 10 },
  { productId: "p-simit2", sku: "SIM-02", name: "Simit Peynirli", categoryCode: "ANA", categoryName: "Ana Yemek", unitPrice: 15, taxRate: 10 },
];

let root: Root | null = null;
afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function openCashier() {
  const added: Array<{ productId: string }> = [];
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
    if (path.endsWith("/catalog")) return jsonResponse(catalog);
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
  await act(async () => { root!.render(createElement(RouterProvider, null, createElement(Cashier))); });
  for (let i = 0; i < 3; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  return added;
}

const searchBox = () => document.querySelector<HTMLInputElement>('input[aria-label="Ürün ara"]')!;
const typeAndEnter = async (text: string, key = "Enter") => {
  await act(async () => {
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(searchBox(), text);
    searchBox().dispatchEvent(new Event("input", { bubbles: true }));
  });
  await act(async () => {
    searchBox().dispatchEvent(new KeyboardEvent("keydown", { key, bubbles: true, cancelable: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));
  });
};

describe("Cashier search is ready for a scanner or the keyboard", () => {
  it("has the focus in the search box when the sales screen opens", async () => {
    await openCashier();

    expect(document.activeElement).toBe(searchBox());
  });

  it("leaves the on-screen keyboard closed on a touch terminal", async () => {
    vi.stubGlobal("matchMedia", (query: string) => ({ matches: query === "(pointer: coarse)" }));
    await openCashier();

    expect(document.activeElement).not.toBe(searchBox());
  });

  it("adds the product whose SKU matches exactly, from any category, and clears the box", async () => {
    const added = await openCashier();

    await typeAndEnter("ayr-01");

    expect(added.map((item) => item.productId)).toEqual(["p-ayran"]);
    expect(searchBox().value).toBe("");
  });

  it("finds an exact SKU of another category while a category tab is open", async () => {
    const added = await openCashier();
    const tab = [...document.querySelectorAll<HTMLButtonElement>('[role="tab"]')].find((candidate) => candidate.textContent?.includes("Ana Yemek"))!;
    await act(async () => { tab.dispatchEvent(new MouseEvent("click", { bubbles: true })); });

    await typeAndEnter("ayr-01");

    expect(added.map((item) => item.productId)).toEqual(["p-ayran"]);
  });

  it("adds the only match of the search", async () => {
    const added = await openCashier();

    await typeAndEnter("peynir");

    expect(added.map((item) => item.productId)).toEqual(["p-simit2"]);
  });

  it("matches the SKU exactly even when it is also part of other products' text", async () => {
    const added = await openCashier();

    await typeAndEnter("sim-01");

    expect(added.map((item) => item.productId)).toEqual(["p-simit"]);
  });

  it("adds nothing when several products match and keeps the text", async () => {
    const added = await openCashier();

    await typeAndEnter("simit");

    expect(added).toEqual([]);
    expect(searchBox().value).toBe("simit");
  });

  it("adds nothing for an empty box or another key", async () => {
    const added = await openCashier();

    await typeAndEnter("");
    await typeAndEnter("ayr-01", "a");

    expect(added).toEqual([]);
  });

  it("opens the options dialog for a product that has options instead of adding it", async () => {
    const added = await openCashier();

    await typeAndEnter("pide-1");

    expect(added).toEqual([]);
    expect(document.querySelector('[role="dialog"]')?.textContent).toContain("Porsiyon boyu");
  });
});
