// @vitest-environment jsdom

import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

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

const order = {
  displayId: "d", terminalId: "terminal", orderId: "order-1", revision: 3, state: "Active", editable: true, orderNumber: "1",
  lines: [
    { itemId: "line-1", name: "Pide", quantity: 2, unitPrice: 100, lineTotal: 200, discountAmount: 0 },
    { itemId: "line-2", name: "Ayran", quantity: 1, unitPrice: 20, lineTotal: 20, discountAmount: 0 },
  ],
  subtotal: 220, discountTotal: 0, taxTotal: 20, total: 220, currency: "TRY", serverTimestamp: "", message: "",
};

let root: Root | null = null;
beforeEach(() => { vi.stubGlobal("open", vi.fn()); });
afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function openTill({ compact = false } = {}): Promise<string[]> {
  const calls: string[] = [];
  vi.stubGlobal("matchMedia", (query: string) => ({
    matches: compact && query.includes("max-width"), media: query, addEventListener: () => undefined, removeEventListener: () => undefined,
    addListener: () => undefined, removeListener: () => undefined, onchange: null, dispatchEvent: () => false,
  }));
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    const method = init?.method ?? "GET";
    calls.push(`${method} ${path.replace(/^.*\/api\/v1\/terminals\/[^/]+/, "")}`);
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities: ["orders.create", "payments.take", "reports.view", "catalog.manage"] });
    if (path.endsWith("/catalog")) return jsonResponse([]);
    if (path.endsWith("/orders/active")) return jsonResponse(order);
    if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
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

const tap = (element: Element) => act(async () => { element.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
const buttons = (label: string) => [...document.querySelectorAll("button")].filter((b) => b.textContent === label);
const deletes = (calls: string[]) => calls.filter((call) => call.startsWith("DELETE"));

describe("removing an order line", () => {
  it("asks first, and nothing is deleted until the cashier confirms", async () => {
    const calls = await openTill();

    await tap(buttons("Kaldır")[0]);

    expect(document.body.textContent).toContain("Kaldırılsın mı?");
    expect(deletes(calls)).toHaveLength(0);
    expect(buttons("Evet, kaldır")).toHaveLength(1);
    await tap(buttons("Evet, kaldır")[0]);
    expect(deletes(calls)).toEqual(["DELETE /orders/order-1/items/line-1?expectedRevision=3"]);
  });

  it("goes back to the plain button when the cashier backs out, and asks about one line at a time", async () => {
    const calls = await openTill();

    await tap(buttons("Kaldır")[0]);
    await tap(buttons("Kaldır")[0]);
    expect(buttons("Evet, kaldır")).toHaveLength(1);
    await tap(buttons("Vazgeç")[0]);

    expect(document.body.textContent).not.toContain("Kaldırılsın mı?");
    expect(buttons("Kaldır")).toHaveLength(2);
    expect(deletes(calls)).toHaveLength(0);
  });

  it("puts the focus on the safe answer when the question appears", async () => {
    await openTill();

    await tap(buttons("Kaldır")[0]);

    expect(document.activeElement).toBe(buttons("Vazgeç")[0]);
  });

  it("makes the confirmation buttons at least 48 px high in the stylesheet", async () => {
    const css = readFileSync(resolve(process.cwd(), "src/styles.css"), "utf8");
    expect(css).toMatch(/\.remove-confirm button\s*\{[^}]*min-height:\s*48px/);
  });
});
