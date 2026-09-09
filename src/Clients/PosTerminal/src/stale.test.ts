// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { DisplaySnapshot } from "./contracts";
import { afterFailure, afterSnapshot, displayPresentation, exposesMoney } from "./stale";
import { App } from "./App";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const snapshot: DisplaySnapshot = {
  displayId: "display",
  terminalId: "terminal",
  orderId: "order",
  revision: 3,
  state: "Active",
  editable: true,
  orderNumber: "POS-1",
  lines: [],
  subtotal: 10,
  discountTotal: 0,
  taxTotal: 2,
  total: 12,
  currency: "TRY",
  serverTimestamp: "2026-08-24T00:00:00Z",
  message: "Aktif",
};

describe("customer display freshness", () => {
  it("keeps the last snapshot under ten seconds with a connection warning", () => {
    const fresh = afterSnapshot(snapshot, 1_000);
    const failed = afterFailure(fresh, 10_999);
    expect(failed.snapshot).toBe(snapshot);
    expect(failed.connectionLost).toBe(true);
  });

  it("clears stale monetary data at ten seconds", () => {
    const fresh = afterSnapshot(snapshot, 1_000);
    const failed = afterFailure(fresh, 11_000);
    expect(failed.snapshot).toBeNull();
    expect(failed.connectionLost).toBe(true);
  });

  it.each([
    ["Idle", "idle", false],
    ["Active", "active", true],
    ["Paying", "paying", true],
    ["Completed", "completed", false],
    ["Unavailable", "unavailable", false],
  ] as const)("maps %s to an explicit presentation without leaking money", (state, expected, money) => {
    const presentation = displayPresentation({ ...snapshot, state });
    expect(presentation).toBe(expected);
    expect(exposesMoney(presentation)).toBe(money);
  });
});

const jsonResponse = (body: unknown, status = 200) => new Response(JSON.stringify(body), {
  status,
  headers: { "Content-Type": "application/json" },
});

async function settle() {
  await new Promise((resolve) => window.setTimeout(resolve, 40));
}

async function criticalAxeViolations() {
  const report = await axe.run(document, {
    rules: { "color-contrast": { enabled: false } },
  });
  return report.violations.filter((violation) =>
    violation.impact === "critical" || violation.impact === "serious");
}

describe("PosTerminal accessibility states", () => {
  let root: Root | null = null;

  async function renderApp() {
    document.documentElement.lang = "tr";
    document.title = "ALKAROS POS";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    // V1-RMD-139: App's routes are now React.lazy() (code-splitting, so an
    // NFC visitor's browser never downloads Cashier/admin code). The lazy
    // import must resolve and the Suspense boundary must re-render BEFORE
    // the resolved route's own effects (its fetch calls, etc.) can even
    // start — a plain synchronous component never needed that extra round
    // trip. Each settle() round runs in its OWN act() call (rather than
    // one act() wrapping all of them) so React actually commits and flushes
    // to the DOM between rounds instead of batching every intermediate
    // state change until the whole loop finishes.
    await act(async () => {
      root!.render(createElement(App));
    });
    for (let round = 0; round < 10; round += 1) {
      await act(async () => {
        await settle();
      });
    }
  }

  afterEach(async () => {
    if (root) await act(async () => root!.unmount());
    root = null;
    vi.restoreAllMocks();
    localStorage.clear();
  });

  it("renders an actionable bounded error without serious semantic violations", async () => {
    window.history.replaceState({}, "", "/display");
    vi.stubGlobal("fetch", vi.fn(async () => jsonResponse({
      error: { code: "DATABASE_UNAVAILABLE", message: "Ekran bilgisi geçici olarak alınamıyor." },
    }, 503)));

    await renderApp();

    expect(document.querySelector('[role="alert"]')?.textContent).toContain("Tekrar dene");
    expect(await criticalAxeViolations()).toEqual([]);
  });

  it("gives the pairing dialog an accessible modal contract", async () => {
    window.history.replaceState({}, "", "/");
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
      }
      if (path.endsWith("/catalog")) return jsonResponse([]);
      if (path.endsWith("/orders/active")) return jsonResponse({ ...snapshot, editable: true });
      if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
      return jsonResponse({});
    }));

    await renderApp();
    const trigger = [...document.querySelectorAll("button")].find((button) =>
      button.textContent?.includes("Ekranı eşleştir"));
    expect(trigger).toBeDefined();
    await act(async () => trigger!.dispatchEvent(new MouseEvent("click", { bubbles: true })));

    const dialog = document.querySelector('[role="dialog"]');
    expect(dialog?.getAttribute("aria-modal")).toBe("true");
    expect(dialog?.getAttribute("aria-labelledby")).toBe("pairing-title");
    expect(await criticalAxeViolations()).toEqual([]);
  });
});
