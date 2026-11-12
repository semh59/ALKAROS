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

const catalog = [{ productId: "p-su", sku: "SKU-1", name: "Su", categoryCode: "ICE", categoryName: "İçecek", unitPrice: 10, taxRate: 10 }];
const tableOrder = {
  displayId: "d", terminalId: "terminal", orderId: "order-table", revision: 3, state: "Active", editable: false, acceptsRound: true,
  orderNumber: "POS-20261112-082500-ABCDEF", tableNumber: "M-10",
  lines: [{ lineId: "l-1", name: "Çay", quantity: 1, unitPrice: 45, lineTotal: 45, discountTotal: 0 }],
  subtotal: 45, discountTotal: 0, taxTotal: 0, total: 45, currency: "TRY", serverTimestamp: "", message: "",
  paidTotal: 0, remainingTotal: null, tipTotal: 0, feeTotal: 0,
};

let root: Root | null = null;
afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
  sessionStorage.clear();
  window.history.replaceState(null, "", "/");
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function openTill(active: Record<string, unknown> = tableOrder, startStatus = 201) {
  const calls: string[] = [];
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    calls.push(`${init?.method ?? "GET"} ${path.replace(/^.*\/api\/v1\/terminals\/[^/]+/, "")}`);
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities: ["orders.create"] });
    if (path.endsWith("/catalog")) return jsonResponse(catalog);
    if (path.endsWith("/orders/active")) return jsonResponse(active);
    if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
    if (path.endsWith("/orders") && init?.method === "POST") {
      return startStatus === 409
        ? jsonResponse({ error: { code: "CONFLICT", message: "Bu kasada başka bir siparişin taslağı açık." } }, 409)
        : jsonResponse({ orderId: "order-quick", orderNumber: "7", revision: 1 }, 201);
    }
    return jsonResponse({ orderId: "order-quick", revision: 2 });
  }));
  const { Cashier } = await import("./Cashier");
  const { RouterProvider } = await import("../router");
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  await act(async () => { root!.render(createElement(RouterProvider, null, createElement(Cashier))); });
  for (let i = 0; i < 3; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  return calls;
}

const tapProduct = async (name: string) => {
  const card = [...document.querySelectorAll<HTMLButtonElement>(".product-card")].find((b) => b.textContent?.includes(name))!;
  await act(async () => { card.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
  await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
};

describe("a table order and the quick sale cart", () => {
  it("does not show a table order the cashier did not just open from the tables screen", async () => {
    await openTill();

    expect(document.body.textContent).not.toContain("Çay");
  });

  it("starts a new quick order on a product tap and never writes into the table order", async () => {
    const calls = await openTill();

    await tapProduct("Su");

    expect(calls).toContain("POST /orders");
    expect(calls.some((call) => call.includes("order-table"))).toBe(false);
  });

  it("shows the table order after the cashier opens it on the tables screen and comes back", async () => {
    await openTill();
    const go = async (path: string) => {
      window.history.pushState(null, "", path);
      await act(async () => { window.dispatchEvent(new PopStateEvent("popstate")); });
      await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
    };
    await go("/zzz");
    sessionStorage.setItem("alkaros.table-cart-order", "order-table");

    await go("/");

    expect(document.body.textContent).toContain("Çay");
    window.history.pushState(null, "", "/");
  });

  it("explains instead of writing into the hidden table draft when a quick sale is refused", async () => {
    const calls = await openTill({ ...tableOrder, editable: true }, 409);

    await tapProduct("Su");

    expect(calls.some((call) => call.includes("order-table"))).toBe(false);
    expect(document.body.textContent).toContain("Masalar ekranından devam edin");
  });

  it("lets the cashier leave the table order for a quick sale with one tap", async () => {
    sessionStorage.setItem("alkaros.table-cart-order", "order-table");
    const calls = await openTill();
    const leave = [...document.querySelectorAll("button")].find((b) => b.textContent === "Hızlı satışa geç")!;

    await act(async () => { leave.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
    await tapProduct("Su");

    expect(document.body.textContent).not.toContain("Çay");
    expect(calls).toContain("POST /orders");
    expect(calls.some((call) => call.includes("order-table"))).toBe(false);
  });

  it("shows the table order, named by its table, when it was opened from the tables screen", async () => {
    sessionStorage.setItem("alkaros.table-cart-order", "order-table");

    await openTill();

    expect(document.body.textContent).toContain("Çay");
    expect([...document.querySelectorAll("h2")].some((heading) => heading.textContent?.includes("Masa M-10"))).toBe(true);
    expect(document.querySelector(".eyebrow.dark")?.textContent).toBe("MASA M-10");
  });
});
