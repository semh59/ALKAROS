// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown) => new Response(JSON.stringify(body), { status: 200, headers: { "content-type": "application/json" } });
const names = ["Isparta Gülü", "İstanbul Simit", "Iğdır Kebap", "Çorba"];

let root: Root | null = null;
afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

describe("Cashier product search folds the Turkish dotted and dotless i", () => {
  it.each([
    ["isparta", ["Isparta Gülü"]],
    ["ISPARTA", ["Isparta Gülü"]],
    ["ıspar", ["Isparta Gülü"]],
    ["istanbul", ["İstanbul Simit"]],
    ["iğdır", ["Iğdır Kebap"]],
    ["İSTANBUL", ["İstanbul Simit"]],
    ["kiz", ["Çorba"]],
  ])("finds %s", async (query, expected) => {
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
      if (path.endsWith("/catalog")) return jsonResponse(names.map((name, index) => ({ productId: `p${index}`, sku: index === 3 ? "KIZ-9" : `SKU-${index}`, name, categoryCode: "ANA", categoryName: "Ana", unitPrice: 10, taxRate: 0.1 })));
      if (path.endsWith("/orders/active")) return jsonResponse(null);
      if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
      return jsonResponse({});
    }));
    const { RouterProvider } = await import("../router");
    const { Cashier } = await import("./Cashier");
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => { root!.render(createElement(RouterProvider, null, createElement(Cashier))); });
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });

    const input = document.querySelector<HTMLInputElement>('input[aria-label="Ürün ara"]')!;
    await act(async () => {
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(input, query);
      input.dispatchEvent(new Event("input", { bubbles: true }));
    });

    const shown = names.filter((name) => document.body.textContent!.includes(name));
    expect(shown).toEqual(expected);
  });
});
