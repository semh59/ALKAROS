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

// Without this link the management area was reachable only after moving to another workspace, because the home
// header did not list it.
async function renderHome(capabilities: string[]) {
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input);
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities });
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
  await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
}

const managementLink = () => [...document.querySelectorAll("a.header-action")].find((link) => link.textContent === "Yönetim");

describe("Cashier home header management link", () => {
  it("is shown to a session with reports.view and points to the management area", async () => {
    await renderHome(["orders.create", "reports.view"]);
    expect(managementLink()?.getAttribute("href")).toBe("/management");
  });

  it("is not shown without reports.view", async () => {
    await renderHome(["orders.create"]);
    expect(managementLink()).toBeUndefined();
  });
});
