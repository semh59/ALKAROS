// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

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

async function renderHome(drawer: "own" | "closed" | "other", status: unknown = { enabled: true, canKick: true }) {
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input);
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities: ["orders.create", "cash.drawer"] });
    if (path.endsWith("/cash-drawer")) return jsonResponse(status);
    if (path.endsWith("/cash-sessions/active")) return drawer === "closed" ? jsonResponse({}, 404) : jsonResponse({ cashSessionId: "s-1", cashierUserId: drawer === "own" ? "user" : "someone-else" });
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
  for (let i = 0; i < 3; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  await act(async () => { (document.querySelector(".pos-header button.operator-card") as HTMLButtonElement).dispatchEvent(new MouseEvent("click", { bubbles: true })); });
}

const noSaleLink = () => [...document.querySelectorAll<HTMLAnchorElement>(".header-menu a")].find((link) => link.textContent === "Satış yok");

describe("the sale screen's Satış yok link", () => {
  it("is in the menu when the own drawer is open and the server says it can be opened, and leads to the reason form on the drawer page", async () => {
    await renderHome("own");

    expect(noSaleLink()?.getAttribute("href")).toBe("/cashier/payments/cash-session/index.html?returnTo=%2F&open=no-sale");
  });

  it.each([
    ["the user may not open the drawer", "own", { enabled: true, canKick: false }],
    ["the drawer is switched off", "own", { enabled: false, canKick: true }],
    ["the drawer is closed", "closed", { enabled: true, canKick: true }],
    ["the session is someone else's", "other", { enabled: true, canKick: true }],
  ] as const)("is not offered when %s", async (_name, drawer, status) => {
    await renderHome(drawer, status);

    expect(noSaleLink()).toBeUndefined();
  });
});
