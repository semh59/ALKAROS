// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const json = (body: unknown, status = 200) =>
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

const burger = (itemId: string, options?: { name: string; price: number }[]) => ({
  itemId, name: "Hamburger", quantity: 1, unitPrice: 150, lineTotal: options?.some((option) => option.price > 0) ? 165 : 150, discountAmount: 0,
  productId: "p-burger", unsent: true, ...(options ? { options } : {}),
});
const order = (lines: unknown[]) => ({
  displayId: "d", terminalId: "terminal", orderId: "order-1", revision: 3, state: "Active", editable: true,
  orderNumber: "1", lines, subtotal: 0, discountTotal: 0, taxTotal: 0, total: 0, currency: "TRY", serverTimestamp: "", message: "",
});

let root: Root | null = null;

afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function openTill(lines: unknown[]) {
  vi.stubGlobal("matchMedia", () => ({ matches: false, media: "", addEventListener: () => undefined, removeEventListener: () => undefined, addListener: () => undefined, removeListener: () => undefined, onchange: null, dispatchEvent: () => false }));
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input);
    if (path.includes("/auth/session")) return json({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities: ["orders.create"] });
    if (path.endsWith("/catalog")) return json([]);
    if (path.endsWith("/orders/active")) return json(order(lines));
    if (path.endsWith("/health/ready")) return json({ status: "ready" });
    return json({});
  }));
  const { Cashier } = await import("./Cashier");
  const { RouterProvider } = await import("../router");
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  await act(async () => { root!.render(createElement(RouterProvider, null, createElement(Cashier))); });
  for (let i = 0; i < 3; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
}

const optionsOf = () => [...document.querySelectorAll(".ticket-line")].map((line) => line.querySelector(".ticket-line-options")?.textContent ?? null);

describe("the till's cart line options", () => {
  it("names each line's options with their extra charge, so two burgers with different options can be told apart", async () => {
    await openTill([
      burger("l1", [{ name: "Az acılı", price: 0 }, { name: "Ekstra peynir", price: 15 }]),
      burger("l2", [{ name: "Sade <b>", price: 0 }]),
      burger("l3"),
    ]);

    expect(optionsOf()).toEqual(["Az acılı, Ekstra peynir +₺15,00", "Sade <b>", null]);
    expect(document.querySelector(".ticket-line b")).toBeNull();
  });
});
