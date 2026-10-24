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

describe("Cashier refreshes only the order after a sale action", () => {
  it("does not read the session, catalog or runtime config again after opening an order", async () => {
    const seen: string[] = [];
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      seen.push(`${init?.method ?? "GET"} ${path}`);
      if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
      if (path.endsWith("/catalog")) return jsonResponse([]);
      if (path.endsWith("/orders/active")) return jsonResponse(null);
      if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
      if (path.endsWith("/orders") && init?.method === "POST") return jsonResponse({ orderId: "order-1", orderNumber: "1", revision: 1 });
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

    const openOrder = [...document.querySelectorAll("button")].find((b) => b.textContent === "Sipariş aç");
    expect(openOrder).toBeTruthy();
    seen.length = 0;
    await act(async () => {
      openOrder!.dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await new Promise((resolve) => setTimeout(resolve, 0));
    });

    expect(seen.filter((call) => call.startsWith("POST") && call.endsWith("/orders"))).toHaveLength(1);
    expect(seen.filter((call) => call.endsWith("/orders/active"))).toHaveLength(1);
    expect(seen.filter((call) => call.includes("/auth/session") || call.endsWith("/catalog") || call.includes("runtime-config"))).toEqual([]);
  });
});
