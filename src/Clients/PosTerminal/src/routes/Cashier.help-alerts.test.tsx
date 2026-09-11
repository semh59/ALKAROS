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

vi.mock("@microsoft/signalr", () => ({
  LogLevel: { Warning: 2 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      return {
        on: (event: string, handler: (payload: unknown) => void) => { handlers[event] = handler; },
        start: startMock,
        stop: stopMock,
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
