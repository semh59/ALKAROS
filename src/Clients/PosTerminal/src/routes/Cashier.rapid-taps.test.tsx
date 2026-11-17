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
  { productId: "p-cay", sku: "SKU-3", name: "Çay", categoryCode: "ICE", categoryName: "İçecek", unitPrice: 10, taxRate: 10 },
];

let root: Root | null = null;
afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

interface Till {
  bodies: { productId: string; quantity: number; expectedRevision: number }[];
  release: () => void;
  releaseReload: () => void;
}

/**
 * The server keeps one open order. Every item POST bumps its revision and answers the new one; the first POST waits for
 * release() and (with holdReload) the read after it waits for releaseReload(), so a test decides what happens in between.
 */
async function openTill({
  failAt = 0,
  failStatus = 500,
  holdReloadAt = 0,
  latency,
}: { failAt?: number; failStatus?: number; holdReloadAt?: number; latency?: (post: number) => number } = {}): Promise<Till> {
  let revision = 4;
  let posts = 0;
  let release: () => void = () => undefined;
  const firstGate = new Promise<void>((resolve) => { release = resolve; });
  let releaseReload: () => void = () => undefined;
  const reloadGate = new Promise<void>((resolve) => { releaseReload = resolve; });
  let reloadHeld = holdReloadAt > 0;
  const till: Till = { bodies: [], release: () => release(), releaseReload: () => releaseReload() };
  const snapshot = () => ({
    displayId: "d", terminalId: "terminal", orderId: "order-open", revision, state: "Active", editable: true, orderNumber: "1",
    lines: [], subtotal: 0, discountTotal: 0, taxTotal: 0, total: 0, currency: "TRY", serverTimestamp: "", message: "",
  });
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    const method = init?.method ?? "GET";
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities: [] });
    if (path.endsWith("/catalog")) return jsonResponse(catalog);
    if (path.endsWith("/orders/active")) {
      const answer = snapshot();
      if (latency) await new Promise((resolve) => setTimeout(resolve, latency(posts)));
      if (reloadHeld && posts === holdReloadAt) { reloadHeld = false; await reloadGate; }
      return jsonResponse(answer);
    }
    if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
    if (path.endsWith("/items") && method === "POST") {
      posts++;
      till.bodies.push(JSON.parse(String(init!.body)));
      if (latency) await new Promise((resolve) => setTimeout(resolve, latency(posts)));
      else if (posts === 1) await firstGate;
      if (posts === failAt) return jsonResponse({ error: { code: "X", message: "sunucu metni" } }, failStatus);
      revision++;
      return jsonResponse({ orderId: "order-open", revision });
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

const settle = async () => { for (let i = 0; i < 4; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); }); };
const tap = (element: Element) => act(async () => { element.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
const card = (name: string) => [...document.querySelectorAll<HTMLButtonElement>(".product-card")].find((b) => b.textContent?.includes(name))!;

describe("Cashier product taps while an add is still in flight", () => {
  it("keeps the grid enabled and sends every tap exactly once, chained on the revision each answer returned", async () => {
    const till = await openTill();

    await tap(card("Ayran"));
    expect(card("Ayran").disabled).toBe(false);
    await tap(card("Ayran"));
    await tap(card("Çay"));
    await tap(card("Ayran"));
    till.release();
    await settle();

    expect(till.bodies).toEqual([
      { productId: "p-ayran", quantity: 1, expectedRevision: 4 },
      { productId: "p-ayran", quantity: 2, expectedRevision: 5 },
      { productId: "p-cay", quantity: 1, expectedRevision: 6 },
    ]);
  });

  it("tells the cashier how many taps are waiting while the add is in flight", async () => {
    const till = await openTill();

    await tap(card("Ayran"));
    await tap(card("Ayran"));
    await tap(card("Çay"));

    expect(document.body.textContent).toContain("2 dokunuş sıraya alındı");
    till.release();
    await settle();
    expect(document.body.textContent).not.toContain("sıraya alındı");
  });

  it("a tap that lands while the order is being read again is sent on the fresh revision, once", async () => {
    const till = await openTill({ holdReloadAt: 1 });

    await tap(card("Ayran"));
    till.release();
    await settle();
    await tap(card("Çay"));
    till.releaseReload();
    await settle();

    expect(till.bodies).toEqual([
      { productId: "p-ayran", quantity: 1, expectedRevision: 4 },
      { productId: "p-cay", quantity: 1, expectedRevision: 5 },
    ]);
  });

  it("after a failed queued add the rest of the queue is dropped and nothing is resent", async () => {
    const till = await openTill({ failAt: 2 });

    await tap(card("Ayran"));
    await tap(card("Çay"));
    await tap(card("Ayran"));
    till.release();
    await settle();
    await settle();

    expect(till.bodies).toHaveLength(2);
    expect(document.body.textContent).toContain("2 dokunuş eklenmedi");
  });

  it("a tap while the order is read again after a failure starts its own add and is not lost", async () => {
    const till = await openTill({ failAt: 2, holdReloadAt: 2 });

    await tap(card("Ayran"));
    await tap(card("Çay"));
    till.release();
    await settle();
    await tap(card("Ayran"));
    till.releaseReload();
    await settle();

    expect(till.bodies.map((body) => body.productId)).toEqual(["p-ayran", "p-cay", "p-ayran"]);
    expect(document.body.textContent).not.toContain("sıraya alındı");
  });

  it("a revision conflict drops the queue instead of resending it", async () => {
    const till = await openTill({ failAt: 1, failStatus: 409 });

    await tap(card("Ayran"));
    await tap(card("Çay"));
    till.release();
    await settle();
    await settle();

    expect(till.bodies).toHaveLength(1);
  });

  it("a product that needs an option choice stays locked while an add is in flight", async () => {
    const till = await openTill();

    await tap(card("Ayran"));
    expect(card("Pide").disabled).toBe(true);
    till.release();
    await settle();
    expect(card("Pide").disabled).toBe(false);
  });

  it("says so when an option choice is confirmed while a scanned product is still being added, instead of dropping it", async () => {
    const till = await openTill();
    await tap(card("Pide"));
    const search = document.querySelector<HTMLInputElement>('input[aria-label="Ürün ara"]')!;
    await act(async () => {
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(search, "SKU-3");
      search.dispatchEvent(new Event("input", { bubbles: true }));
    });
    await act(async () => { search.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", bubbles: true })); });
    await tap(document.querySelector('input[type="radio"]')!);

    await tap([...document.querySelectorAll<HTMLButtonElement>("button")].find((b) => b.textContent === "Siparişe ekle")!);
    till.release();
    await settle();

    expect(till.bodies.map((body) => body.productId)).toEqual(["p-cay"]);
    expect(document.body.textContent).toContain("Pide eklenmedi");
  });

  it("loses no tap when they come 20-50 ms apart against a server answering in 0-80 ms: every tap is sent once or reported", async () => {
    // Fixed pseudo-random gaps and latencies, so a failure repeats.
    let seed = 7;
    const next = (max: number) => { seed = (seed * 1103515245 + 12345) % 2147483648; return seed % max; };
    const till = await openTill({ latency: () => next(81) });
    const products = ["Ayran", "Çay", "Ayran", "Ayran", "Çay", "Çay", "Ayran", "Çay", "Ayran", "Ayran", "Çay", "Çay"];

    for (const name of products) {
      await tap(card(name));
      await act(async () => { await new Promise((resolve) => setTimeout(resolve, 20 + next(31))); });
    }
    await vi.waitFor(() => expect(document.body.textContent).not.toContain("sıraya alındı"), { timeout: 5000 });
    await settle();

    const sent = new Map<string, number>();
    for (const body of till.bodies) sent.set(body.productId, (sent.get(body.productId) ?? 0) + body.quantity);
    const reported = /(\d+) dokunuş eklenmedi/.exec(document.body.textContent ?? "");
    const tapped = (id: string) => products.filter((name) => (name === "Ayran" ? "p-ayran" : "p-cay") === id).length;
    expect(reported).toBeNull();
    expect(sent.get("p-ayran")).toBe(tapped("p-ayran"));
    expect(sent.get("p-cay")).toBe(tapped("p-cay"));
    // Each request carries the revision the answer before it returned: 4, 5, 6 ...
    expect(till.bodies.map((body) => body.expectedRevision)).toEqual(till.bodies.map((_, index) => 4 + index));
  });
});
