// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

vi.mock("@microsoft/signalr", () => ({
  LogLevel: { Warning: 2 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() { return { on: () => undefined, onreconnecting: () => undefined, onreconnected: () => undefined, onclose: () => undefined, start: vi.fn(async () => undefined), stop: vi.fn(async () => undefined) }; }
  },
}));

const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });

type Line = { itemId: string; name: string; quantity: number; unitPrice: number; lineTotal: number; discountAmount: number; productId?: string; modifierIds?: string[] };
const catalog = [{ productId: "p-pide", sku: "PD", name: "Pide", categoryCode: "ANA", categoryName: "Ana", unitPrice: 100, taxRate: 10 }];
const pideLine = (over: Partial<Line> = {}): Line =>
  ({ itemId: "line-1", name: "Pide", quantity: 2, unitPrice: 100, lineTotal: 200, discountAmount: 0, productId: "p-pide", modifierIds: ["m-peynir"], ...over });

let root: Root | null = null;
beforeEach(() => { vi.stubGlobal("open", vi.fn()); });
afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function openTill(first: Line, failAdd = false) {
  const calls: { method: string; path: string; body: Record<string, unknown> }[] = [];
  let lines: Line[] = [first];
  let revision = 3;
  let nextId = 1;
  const order = () => ({
    displayId: "d", terminalId: "terminal", orderId: "order-1", revision, state: "Active", editable: true, orderNumber: "1", lines,
    subtotal: 0, discountTotal: 0, taxTotal: 0, total: 0, currency: "TRY", serverTimestamp: "", message: "",
  });
  vi.stubGlobal("matchMedia", () => ({ matches: false, media: "", addEventListener: () => undefined, removeEventListener: () => undefined, addListener: () => undefined, removeListener: () => undefined, onchange: null, dispatchEvent: () => false }));
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    const method = init?.method ?? "GET";
    const body = init?.body ? JSON.parse(String(init.body)) as Record<string, unknown> : {};
    if (path.includes("/auth/session")) return json({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities: ["orders.create", "payments.take"] });
    if (path.endsWith("/catalog")) return json(catalog);
    if (path.endsWith("/orders/active")) return json(order());
    if (path.endsWith("/health/ready")) return json({ status: "ready" });
    if (path.includes("/orders/order-1/items")) {
      calls.push({ method, path: path.replace(/^.*\/orders\/order-1/, ""), body });
      if (method === "POST" && failAdd) return json({ error: { code: "FAILED", message: "Ekleme başarısız.", status: 500 } }, 500);
      revision += 1;
      if (method === "DELETE") lines = lines.filter((line) => !path.includes(`/items/${line.itemId}`));
      else if (method === "POST") lines = [...lines, pideLine({ itemId: `new-${nextId++}`, quantity: body.quantity as number, lineTotal: 100, modifierIds: ((body.modifiers as { modifierId: string }[] | undefined) ?? []).map((m) => m.modifierId) })];
      else if (method === "PATCH") lines = lines.map((line) => path.endsWith(line.itemId) ? { ...line, quantity: body.quantity as number } : line);
      return json({ orderId: "order-1", revision });
    }
    return json({});
  }));
  const { Cashier } = await import("./Cashier");
  const { RouterProvider } = await import("../router");
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  await act(async () => { root!.render(createElement(RouterProvider, null, createElement(Cashier))); });
  for (let i = 0; i < 3; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  return { calls, lines: () => lines };
}

const tap = async (element: Element) => {
  await act(async () => { element.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
  for (let i = 0; i < 3; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
};
const button = (label: string) => [...document.querySelectorAll("button")].find((b) => b.textContent === label);
const remove = async () => { await tap(button("Kaldır")!); await tap(button("Evet, kaldır")!); };

describe("putting back a removed order line", () => {
  it("offers Geri al after the removal and rebuilds the line with its options and quantity", async () => {
    const till = await openTill(pideLine());

    await remove();
    expect(till.lines()).toHaveLength(0);
    expect(document.body.textContent).toContain("Kalem kaldırıldı.");
    await tap(button("Geri al")!);

    expect(till.calls.map((call) => `${call.method} ${call.path.split("?")[0]}`)).toEqual(["DELETE /items/line-1", "POST /items"]);
    expect(till.calls[1].body).toMatchObject({ productId: "p-pide", quantity: 2, modifiers: [{ modifierId: "m-peynir" }] });
    expect(till.lines()).toMatchObject([{ productId: "p-pide", quantity: 2, modifierIds: ["m-peynir"] }]);
    expect(document.body.textContent).toContain("Kalem geri alındı.");
    expect(button("Geri al")).toBeUndefined();
  });

  it("shows the server answer when putting the line back fails and leaves no half-rebuilt line", async () => {
    const till = await openTill(pideLine(), true);

    await remove();
    await tap(button("Geri al")!);

    expect(till.lines()).toHaveLength(0);
    expect(document.body.textContent).toContain("Ekleme başarısız.");
    expect(document.body.textContent).not.toContain("Kalem geri alındı.");
  });

  it("offers no undo for a line with its own discount, a line without a product id or a product that left the catalog", async () => {
    for (const line of [pideLine({ discountAmount: 10 }), pideLine({ productId: undefined }), pideLine({ productId: "p-gone" })]) {
      const till = await openTill(line);
      await remove();
      expect(till.calls.map((call) => call.method)).toEqual(["DELETE"]);
      expect(document.body.textContent).toContain("Kalem kaldırıldı.");
      expect(button("Geri al")).toBeUndefined();
      act(() => root!.unmount());
      root = null;
      vi.resetModules();
    }
  });

  it("closes the offer when another action runs", async () => {
    await openTill(pideLine());
    await remove();
    expect(button("Geri al")).toBeDefined();

    await tap(document.querySelector(".product-card")!);

    expect(button("Geri al")).toBeUndefined();
  });
});
