// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const chime = vi.hoisted(() => vi.fn());
vi.mock("../audioAlerts", () => ({ playNewItemChime: chime }));
vi.mock("@microsoft/signalr", () => ({
  LogLevel: { Warning: 2 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      return { on: () => undefined, onreconnecting: () => undefined, onreconnected: () => undefined, onclose: () => undefined, start: async () => undefined, stop: async () => undefined };
    }
  },
}));

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });

const queueOf = (...ids: string[]) =>
  jsonResponse({ orders: ids.map((orderId) => ({ orderId })), problems: [], retries: {} });

let root: Root | null = null;

afterEach(() => {
  vi.useRealTimers();
  chime.mockClear();
  if (root) {
    act(() => root!.unmount());
    root = null;
  }
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

async function renderSalesScreen(capabilities: string[], queues: Response[]) {
  const queueUrls: string[] = [];
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input);
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities });
    if (path.includes("/online-operations")) { queueUrls.push(path); return queues.shift() ?? queueOf(); }
    if (path.endsWith("/catalog")) return jsonResponse([]);
    if (path.endsWith("/orders/active")) return jsonResponse(null);
    if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
    return jsonResponse({});
  }));
  const { RouterProvider } = await import("../router");
  const { Cashier } = await import("./Cashier");
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  await act(async () => { root!.render(createElement(RouterProvider, null, createElement(Cashier))); });
  await act(async () => { await vi.advanceTimersByTimeAsync(0); });
  await act(async () => { await vi.advanceTimersByTimeAsync(0); });
  return queueUrls;
}

describe("Cashier new order sound", () => {
  it("chimes on the sales screen when an order the till has not seen lands, and only after the first read", async () => {
    vi.useFakeTimers();
    const urls = await renderSalesScreen(["orders.create"], [queueOf("a"), queueOf("a"), queueOf("a", "b")]);
    expect(urls[0]).toContain("source=all");
    expect(chime).not.toHaveBeenCalled();

    await act(async () => { await vi.advanceTimersByTimeAsync(20_000); });
    expect(chime).not.toHaveBeenCalled();
    await act(async () => { await vi.advanceTimersByTimeAsync(20_000); });
    expect(chime).toHaveBeenCalledTimes(1);
  });

  it("does not watch the queue for a session that cannot accept orders", async () => {
    vi.useFakeTimers();
    const urls = await renderSalesScreen(["tables.status"], [queueOf("a")]);
    await act(async () => { await vi.advanceTimersByTimeAsync(40_000); });
    expect(urls).toEqual([]);
  });
});
