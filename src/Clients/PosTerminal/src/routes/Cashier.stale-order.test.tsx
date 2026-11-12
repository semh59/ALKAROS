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

const line = { lineId: "l-1", name: "Eski Ayran", quantity: 1, unitPrice: 20, lineTotal: 20, discountTotal: 0 };
const finished = {
  displayId: "d", terminalId: "terminal", orderId: "order-old", revision: 9, state: "Active", editable: false, orderNumber: "7",
  lines: [line], subtotal: 20, discountTotal: 0, taxTotal: 0, total: 20, currency: "TRY", serverTimestamp: "", message: "",
  paidTotal: 0, remainingTotal: null, tipTotal: 0, feeTotal: 0,
};

let root: Root | null = null;
afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function openTill(active: Record<string, unknown>) {
  const calls: string[] = [];
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    calls.push(`${init?.method ?? "GET"} ${path.replace(/^.*\/api\/v1\/terminals\/[^/]+/, "")}`);
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities: ["orders.create"] });
    if (path.endsWith("/catalog")) return jsonResponse([{ productId: "p-ayran", sku: "SKU-2", name: "Ayran", categoryCode: "ICE", categoryName: "İçecek", unitPrice: 20, taxRate: 10 }]);
    if (path.endsWith("/orders/active")) return jsonResponse(active);
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

describe("Cashier cart never keeps a finished order", () => {
  it.each([
    ["a completed order", { ...finished, state: "Completed" }],
    ["a cancelled order", { ...finished, state: "Idle" }],
    ["a fully paid order", { ...finished, paidTotal: 20, remainingTotal: 0 }],
  ])("opens clean for %s", async (_name, active) => {
    await openTill(active);

    expect(document.body.textContent).not.toContain("Eski Ayran");
  });

  it("opens a new order on the first product tap and never adds to the finished one", async () => {
    const calls = await openTill({ ...finished, state: "Completed" });

    const card = [...document.querySelectorAll<HTMLButtonElement>(".product-card")].find((b) => b.textContent?.includes("Ayran"))!;
    await act(async () => { card.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });

    expect(calls).toContain("POST /orders");
    expect(calls.some((c) => c.includes("order-old"))).toBe(false);
  });

  it("still shows an order that is partly paid", async () => {
    await openTill({ ...finished, paidTotal: 5, remainingTotal: 15 });

    expect(document.body.textContent).toContain("Eski Ayran");
  });

  it("still shows an order that is waiting for payment", async () => {
    await openTill(finished);

    expect(document.body.textContent).toContain("Eski Ayran");
  });
});
