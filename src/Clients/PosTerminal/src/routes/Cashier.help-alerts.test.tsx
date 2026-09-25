// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RouterProvider } from "../router";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });

// V1-WTR-014: captures whatever handler Cashier registers for
// "HelpRequested" so the test can fire it directly instead of standing up a
// real WebSocket - the same reason no other SignalR usage in this codebase
// is unit-tested at this layer (see Cashier.tsx's own note on this), but
// this feature's one real risk (an event-name or payload-field typo between
// the server's HelpRequestedV1 and this file) is exactly what a mock like
// this catches and a typecheck cannot.
const handlers: Record<string, (payload: unknown) => void> = {};
const startMock = vi.fn(async () => undefined);
const stopMock = vi.fn(async () => undefined);
const lifecycle: { reconnecting?: () => void; reconnected?: () => void; closed?: () => void } = {};

vi.mock("@microsoft/signalr", () => ({
  LogLevel: { Warning: 2 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      return {
        on: (event: string, handler: (payload: unknown) => void) => { handlers[event] = handler; },
        onreconnecting: (callback: () => void) => { lifecycle.reconnecting = callback; },
        onreconnected: (callback: () => void) => { lifecycle.reconnected = callback; },
        onclose: (callback: () => void) => { lifecycle.closed = callback; },
        start: startMock,
        stop: stopMock,
      };
    }
  },
}));

let root: Root | null = null;

afterEach(() => {
  vi.useRealTimers();
  startMock.mockReset();
  startMock.mockImplementation(async () => undefined);
  lifecycle.reconnecting = lifecycle.reconnected = lifecycle.closed = undefined;
  if (root) {
    act(() => root!.unmount());
    root = null;
  }
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

describe("Cashier help alerts", () => {
  it("renders a table's help request the moment the hub broadcasts it", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
      }
      if (path.endsWith("/catalog")) return jsonResponse([]);
      if (path.endsWith("/orders/active")) return jsonResponse(null);
      if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
      return jsonResponse({});
    }));

    const { Cashier } = await import("./Cashier");

    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => {
      root!.render(createElement(RouterProvider, null, createElement(Cashier)));
    });
    // Flush the microtask queue so restoreSession's awaited fetches settle
    // and the SignalR effect (gated on session === "ready") actually runs.
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });

    expect(startMock).toHaveBeenCalled();
    expect(handlers.HelpRequested).toBeDefined();

    await act(async () => {
      handlers.HelpRequested({
        tableId: "table-5",
        tableNumber: "T-5",
        requestType: "Complaint",
        requestedByDisplayName: "Ayşe",
      });
    });

    const alert = document.querySelector(".help-alert");
    expect(alert?.textContent).toContain("T-5 masası");
    expect(alert?.textContent).toContain("Misafir şikayeti");
    expect(alert?.textContent).toContain("Ayşe");

    const dismiss = alert!.querySelector("button")!;
    await act(async () => { dismiss.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
    expect(document.querySelector(".help-alert")).toBeNull();
  });
});

// V1-RMD-286: the connection used to give up for good after five automatic
// retries and swallow a failed first start(); a link that stayed down left the
// cashier without any sign that help calls could no longer arrive.
describe("Cashier help connection resilience", () => {
  const sessionFetch = () => vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input);
    if (path.includes("/auth/session")) {
      return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
    }
    if (path.endsWith("/catalog")) return jsonResponse([]);
    if (path.endsWith("/orders/active")) return jsonResponse(null);
    if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
    return jsonResponse({});
  });

  const tick = (ms: number) => act(async () => { await vi.advanceTimersByTimeAsync(ms); });

  async function renderCashier() {
    vi.useFakeTimers();
    vi.stubGlobal("fetch", sessionFetch());
    // vi.resetModules() in afterEach gives each test a fresh router module, so
    // the provider must come from the same import as Cashier.
    const { Cashier } = await import("./Cashier");
    const { RouterProvider: FreshRouterProvider } = await import("../router");
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => {
      root!.render(createElement(FreshRouterProvider, null, createElement(Cashier)));
    });
    await tick(0);
    await tick(0);
  }

  it("keeps retrying when the first start() fails instead of giving up", async () => {
    startMock.mockRejectedValueOnce(new Error("unreachable")).mockRejectedValueOnce(new Error("unreachable"));
    await renderCashier();
    expect(startMock).toHaveBeenCalledTimes(1);
    await tick(1_000);
    expect(startMock).toHaveBeenCalledTimes(2);
    await tick(2_000);
    expect(startMock).toHaveBeenCalledTimes(3);
    // A session that never became established shows no status at all.
    expect(document.body.textContent).not.toContain("Yardım çağrısı bağlantısı");
  });

  it("tells the cashier when an established link drops and that calls may have been missed after it returns", async () => {
    await renderCashier();
    await tick(5_000);

    await act(async () => { lifecycle.reconnecting?.(); });
    expect(document.body.textContent).toContain("Yardım çağrısı bağlantısı koptu, yeniden bağlanılıyor.");

    await act(async () => { lifecycle.reconnected?.(); });
    expect(document.body.textContent).toContain("gelen yardım çağrıları görülmemiş olabilir");

    const dismiss = document.querySelector(".help-alert button")!;
    await act(async () => { dismiss.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
    expect(document.body.textContent).not.toContain("görülmemiş olabilir");
  });

  it("restarts the connection after it closes and stays quiet for a session the hub refuses", async () => {
    await renderCashier();
    // The hub aborts a cashier-only session right after connecting.
    await act(async () => { lifecycle.closed?.(); });
    expect(document.body.textContent).not.toContain("Yardım çağrısı bağlantısı");
    const before = startMock.mock.calls.length;
    await tick(1_000);
    expect(startMock.mock.calls.length).toBe(before + 1);
    // Backoff was not reset by the short-lived connection: next wait is 2 s.
    await act(async () => { lifecycle.closed?.(); });
    await tick(1_000);
    expect(startMock.mock.calls.length).toBe(before + 1);
    await tick(1_000);
    expect(startMock.mock.calls.length).toBe(before + 2);
  });
});
