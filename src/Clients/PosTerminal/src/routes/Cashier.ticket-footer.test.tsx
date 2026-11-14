// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { readFileSync } from "node:fs";
import { markTableCart } from "../tableCart";
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

const catalog = [{ productId: "p-ayran", sku: "SKU-2", name: "Ayran", categoryCode: "ICE", categoryName: "İçecek", unitPrice: 20, taxRate: 10 }];
const line = (n: number, quantity = 1) => ({ itemId: `i-${n}`, name: `Ürün ${n}`, quantity, unitPrice: 20, lineTotal: 20 * quantity, discountAmount: 0 });
const order = (over: Record<string, unknown>) => ({
  displayId: "d", terminalId: "terminal", orderId: "order-1", revision: 5, state: "Active", editable: false, orderNumber: "1",
  lines: Array.from({ length: 12 }, (_, i) => line(i)), subtotal: 240, discountTotal: 0, taxTotal: 0, total: 240, currency: "TRY", serverTimestamp: "", message: "", ...over,
});

let root: Root | null = null;
afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function openTill(active: Record<string, unknown>) {
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input);
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities: ["orders.create", "orders.add-without-kitchen", "bills.split"] });
    if (path.endsWith("/catalog")) return jsonResponse(catalog);
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
}

const button = (label: string) => [...document.querySelectorAll<HTMLButtonElement>("button")].find((b) => b.textContent?.includes(label));

describe("the order ticket keeps its actions in a fixed footer", () => {
  it("keeps Tahsil et and the kitchen-less button outside the scrolling line list of a long sent order", async () => {
    await openTill(order({}));

    const footer = document.querySelector(".ticket-panel > .ticket-footer")!;
    expect(footer).not.toBeNull();
    expect(document.querySelectorAll(".ticket-lines .ticket-line")).toHaveLength(12);
    expect(footer.contains(button("Tahsil et")!)).toBe(true);
    expect(footer.contains(button("Mutfağa göndermeden hesaba ekle")!)).toBe(true);
    expect(footer.querySelector(".ticket-totals")).not.toBeNull();
  });

  it("counts what the send button sends", async () => {
    await openTill(order({ editable: true, lines: [line(1, 2), line(2)] }));

    expect(button("Mutfağa gönder (3)")).toBeDefined();
    expect(button("Siparişi gönder")).toBeUndefined();
    expect(document.querySelector(".mobile-ticket-bar")).not.toBeNull();
    expect(document.querySelector(".ticket-panel > .ticket-footer")?.contains(button("Mutfağa gönder (3)")!)).toBe(true);
  });

  it("draws no totals and no send button, only the catalogue hint, while the order has no lines", async () => {
    await openTill(order({ editable: true, lines: [], total: 0 }));

    expect(document.querySelector(".ticket-empty")?.textContent).toBe("Soldaki katalogdan ürün seçin.");
    expect(document.querySelector(".ticket-totals")).toBeNull();
    expect(button("Mutfağa gönder")).toBeUndefined();
    expect(document.querySelector(".mobile-ticket-bar")).toBeNull();
  });

  it("names the table in the heading of a table order opened from the table plan", async () => {
    markTableCart("order-1");
    await openTill(order({ editable: true, tableNumber: "T-4", lines: [line(1)] }));

    expect(document.querySelector(".ticket-heading h2")?.textContent).toContain("Masa T-4");
  });

  it("lets only the line list shrink and scroll", () => {
    const css = readFileSync("src/styles.css", "utf8");
    expect(css).toMatch(/\.ticket-lines \{[^}]*overflow-y: auto/);
    expect(css).toMatch(/\.ticket-footer \{[^}]*flex: none/);
    expect(css).toMatch(/\.ticket-footer \{[^}]*max-height: [^;]+;[^}]*overflow-y: auto/);
  });
});
