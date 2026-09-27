// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RouterProvider } from "../router";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });

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

// V1-RMD-364 (module-by-module UI audit, 2026-09-27): the category rail
// filters which view of the same product grid shows - the same tab pattern
// Cashier's vanilla client had (V1-RMD-360) - but carried only a visual
// "active" class, no role="tab"/aria-selected at all.
describe("Cashier category rail tabs (V1-RMD-364)", () => {
  it("exposes role=tablist/tab and moves aria-selected when a category is picked", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
      }
      if (path.endsWith("/catalog")) {
        return jsonResponse([
          { productId: "p1", sku: "SKU-1", name: "Köfte", categoryCode: "ANA", categoryName: "Ana Yemek", unitPrice: 100, taxRate: 0.1 },
          { productId: "p2", sku: "SKU-2", name: "Ayran", categoryCode: "ICE", categoryName: "İçecek", unitPrice: 20, taxRate: 0.1 },
        ]);
      }
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
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });

    const rail = document.querySelector(".category-rail")!;
    expect(rail.getAttribute("role")).toBe("tablist");
    const tabs = [...rail.querySelectorAll("button")];
    expect(tabs.length).toBeGreaterThan(1);
    tabs.forEach((tab) => expect(tab.getAttribute("role")).toBe("tab"));

    // "ALL" (the "all products" tab) is the default.
    expect(tabs[0].getAttribute("aria-selected")).toBe("true");
    expect(tabs[1].getAttribute("aria-selected")).toBe("false");

    await act(async () => { tabs[1].dispatchEvent(new MouseEvent("click", { bubbles: true })); });

    expect(tabs[0].getAttribute("aria-selected")).toBe("false");
    expect(tabs[1].getAttribute("aria-selected")).toBe("true");
  });

  // Cashier.tsx - arguably the highest-traffic screen in the whole system -
  // had no axe-core scan at all, unlike ~13 other feature workspaces
  // (CatalogWorkspace, BillSplitWorkspace, TableWorkspace, etc., see their
  // own "has no critical or serious axe violations" tests). Closing that
  // test-coverage gap here, on the same screen this module's fix landed on.
  it("has no critical or serious axe violations on the main sales screen", async () => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
      }
      if (path.endsWith("/catalog")) {
        return jsonResponse([
          { productId: "p1", sku: "SKU-1", name: "Köfte", categoryCode: "ANA", categoryName: "Ana Yemek", unitPrice: 100, taxRate: 0.1 },
        ]);
      }
      if (path.endsWith("/orders/active")) return jsonResponse(null);
      if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
      return jsonResponse({});
    }));

    // vi.resetModules() in afterEach gives each test a fresh router module
    // (see Cashier.help-alerts.test.tsx's own resilience suite for the same
    // note) - the provider must come from the same import as Cashier.
    const { Cashier } = await import("./Cashier");
    const { RouterProvider: FreshRouterProvider } = await import("../router");
    document.documentElement.lang = "tr";
    document.title = "ALKAROS kasa";
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => {
      root!.render(createElement(FreshRouterProvider, null, createElement(Cashier)));
    });
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });

    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
