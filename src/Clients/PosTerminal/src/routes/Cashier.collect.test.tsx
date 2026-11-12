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

const catalog = [
  { productId: "p-ayran", sku: "SKU-2", name: "Ayran", categoryCode: "ICE", categoryName: "İçecek", unitPrice: 20, taxRate: 10 },
];
const snapshot = (orderId: string, extra: Record<string, unknown> = {}) => ({
  displayId: "d", terminalId: "terminal", orderId, revision: 1, state: "Active", editable: false, orderNumber: "1",
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

async function openTill(capabilities: string[], active: ReturnType<typeof snapshot> | null) {
  const calls: string[] = [];
  const assign = vi.fn();
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    const method = init?.method ?? "GET";
    calls.push(`${method} ${path.replace(/^.*\/api\/v1\/terminals\/[^/]+/, "")}`);
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities });
    if (path.endsWith("/catalog")) return jsonResponse(catalog);
    if (path.endsWith("/orders/active")) return jsonResponse(active);
    if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
    if (path.includes("/billing/bills/from-order/")) return jsonResponse({ billId: "bill-9" });
    return jsonResponse({});
  }));
  const { Cashier } = await import("./Cashier");
  const { RouterProvider } = await import("../router");
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  await act(async () => { root!.render(createElement(RouterProvider, null, createElement(Cashier, { navigateToPage: (href: string) => assign(href) }))); });
  for (let i = 0; i < 3; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  return { calls, assign };
}

const settle = () => act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
const tap = (element: Element) => act(async () => { element.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
const button = (label: string) => [...document.querySelectorAll<HTMLButtonElement>("button")].find((b) => b.textContent === label);

describe("Cashier collection entry on the sales screen", () => {
  it("a sent order offers Tahsil et, which opens its bill and goes to the payment page", async () => {
    const till = await openTill(["bills.split", "orders.create"], snapshot("order-sent"));

    await tap(button("Tahsil et")!);
    await settle();

    expect(till.calls).toContain("POST /billing/bills/from-order/order-sent");
    expect(till.assign).toHaveBeenCalledWith("/cashier/payments/split-payment/index.html?billId=bill-9");
  });

  it("an order still being edited offers no Tahsil et, only sending", async () => {
    await openTill(["bills.split", "orders.create"], snapshot("order-open", { editable: true, lines: [{ lineId: "l", productId: "p-ayran", name: "Ayran", quantity: 1, unitPrice: 20, lineTotal: 20, discountAmount: 0 }], total: 20 }));

    expect(button("Tahsil et")).toBeUndefined();
    expect(button("Mutfağa gönder (1)")).toBeDefined();
  });

  it("without the permission to open a bill the button is not offered and the reason is shown", async () => {
    await openTill(["orders.create"], snapshot("order-sent"));

    expect(button("Tahsil et")).toBeUndefined();
    expect(document.body.textContent).toContain("Bu hesabı açma yetkiniz yok; müdürden hesabı açmasını isteyin.");
  });
});
