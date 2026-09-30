// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

// V1-RMD-374 (module-by-module UI audit, 2026-09-27): mirrors
// Cashier.help-alerts.test.tsx's own SignalR mock - the reason no other
// hub usage in this codebase is unit-tested at a deeper layer than "does it
// call start()".
const startMock = vi.fn(async () => undefined);
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
        start: startMock,
        stop: async () => undefined,
      };
    }
  },
}));

const snapshot = {
  displayId: "d-1", terminalId: "t-1", orderId: "o-1", revision: 3, state: "Active", editable: true,
  orderNumber: "S-042",
  lines: [{ itemId: "i-1", name: "Köfte", quantity: 2, lineTotal: 560 }],
  subtotal: 560, discountTotal: 0, taxTotal: 50.91, total: 560, currency: "TRY",
  serverTimestamp: "2026-09-27T12:00:00Z", message: "Siparişiniz hazırlanıyor.",
};

/**
 * V1-RMD-374 (module-by-module UI audit, 2026-09-27): CustomerDisplay.tsx -
 * the actual customer-facing screen on the till's paired second monitor -
 * had no test file at all.
 */
describe("CustomerDisplay", () => {
  let root: Root | null = null;

  async function render() {
    const { CustomerDisplay } = await import("./CustomerDisplay");
    document.documentElement.lang = "tr";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(createElement(CustomerDisplay)));
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    vi.resetModules();
    startMock.mockClear();
  });

  it("shows the pairing code when the display has no session yet", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ error: { code: "UNAUTHORIZED", message: "Eşleştirme gerekli." } }, 401);
      if (path.includes("/pairing-requests")) {
        return jsonResponse({ requestId: "r-1", displayId: "d-1", secret: "s-1", code: "A7K2M9P4", expiresAt: new Date(Date.now() + 120_000).toISOString() });
      }
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("A7K2M9P4");
    expect(document.body.textContent).toContain("kasaya bağlayın");
  });

  it("renders an active order's lines and total, authoritative from the snapshot", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse(snapshot);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    expect(document.body.textContent).toContain("S-042");
    expect(document.body.textContent).toContain("Köfte");
    expect(document.body.textContent).toContain("560,00");
    expect(document.body.textContent).toContain("İçindeki KDV");
    expect(document.body.textContent).toContain("50,91");
    expect(document.body.textContent).not.toContain("616,00");
    expect(startMock).toHaveBeenCalled();
  });

  it("conceals a completed order's line detail after the thank-you screen, showing the welcome card instead", async () => {
    vi.useFakeTimers();
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse({ ...snapshot, state: "Completed", editable: false });
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());
    expect(document.body.textContent).toContain("Teşekkür ederiz.");

    await act(async () => { await vi.advanceTimersByTimeAsync(7_000); });
    expect(document.body.textContent).not.toContain("Köfte");
    expect(document.body.textContent).toContain("Siparişiniz için hazırız.");
    vi.useRealTimers();
  });

  it("has no critical or serious axe violations on the active order screen", async () => {
    document.title = "ALKAROS müşteri ekranı";
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/snapshot")) return jsonResponse(snapshot);
      throw new Error(`unexpected fetch: ${path}`);
    }));

    await render();
    await act(async () => Promise.resolve());
    await act(async () => Promise.resolve());

    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
