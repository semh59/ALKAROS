// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RouterProvider } from "../router";

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
  localStorage.clear();
});

// V1-RMD-314 (independent 2026-09-26 audit, finding K4): workspace.tsx persists the split-payment
// screen's current bill under "alkaros.current-bill-id" across a reload, but logout() never cleared it -
// a shift change let the NEXT cashier land straight on the PREVIOUS cashier's open split-payment view.
describe("Cashier logout clears session-scoped localStorage", () => {
  it('removes "alkaros.current-bill-id" when the cashier logs out', async () => {
    localStorage.setItem("alkaros.current-bill-id", "11111111-1111-1111-1111-111111111111");

    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/auth/logout")) return jsonResponse(null);
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
    // Flush the microtask queue so restoreSession's awaited fetches settle before we look for the
    // logged-in shell (same pattern Cashier.help-alerts.test.tsx already establishes for this file).
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 0));
    });

    const logoutButton = document.querySelector('[aria-label="Oturumu kapat"]') as HTMLButtonElement | null;
    expect(logoutButton).not.toBeNull();

    expect(localStorage.getItem("alkaros.current-bill-id")).toBe("11111111-1111-1111-1111-111111111111");

    await act(async () => {
      logoutButton!.click();
      await new Promise((resolve) => setTimeout(resolve, 0));
    });

    expect(localStorage.getItem("alkaros.current-bill-id")).toBeNull();
  });
});
