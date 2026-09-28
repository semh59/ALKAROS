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
});

// V1-RMD-388 (Tur 2, T7): a cashier signed in on one terminal whose session
// gets pulled out from under them - a manager revoking their sessions from
// SecurityAdministration.tsx (Module 13), or the cookie simply expiring -
// used to land on the plain login form with zero indication anything
// happened, indistinguishable from having never logged in at all.
describe("Cashier tells the operator why they landed back on the login form", () => {
  it('shows "Oturumunuz sonlandırıldı" when a mid-session call comes back 401, and clears it on the next login attempt', async () => {
    let orderStartShouldFail = false;

    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
      }
      if (path.includes("/auth/login")) {
        return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
      }
      if (path.endsWith("/catalog")) return jsonResponse([]);
      if (path.endsWith("/orders/active")) return jsonResponse(null);
      if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
      if (path.endsWith("/orders") && init?.method === "POST") {
        if (orderStartShouldFail) return jsonResponse({ message: "Unauthorized" }, 401);
        return jsonResponse({ orderId: "order-1", orderNumber: "1", revision: 1 });
      }
      return jsonResponse({});
    }));

    const { Cashier } = await import("./Cashier");

    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => {
      root!.render(createElement(RouterProvider, null, createElement(Cashier)));
    });
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });

    // Signed in, no active order yet: the "open a new order" button is the
    // only way to reach orders/start without a full catalog+ticket setup.
    const openOrderLabel = "Sipariş aç";
    const openOrder = [...document.querySelectorAll("button")].find((b) => b.textContent === openOrderLabel);
    expect(openOrder).toBeTruthy();
    expect(document.body.textContent).not.toContain("Oturumunuz sonlandırıldı");

    orderStartShouldFail = true;
    await act(async () => {
      openOrder!.dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await new Promise((resolve) => setTimeout(resolve, 0));
    });

    // Bounced back to the login form, this time with an explanation.
    expect(document.querySelector('[aria-label="Oturumu kapat"]')).toBeNull();
    expect(document.body.textContent).toContain("Oturumunuz sonlandırıldı. Lütfen tekrar giriş yapın.");

    // Submitting the login form again clears the stale message rather than
    // leaving it stuck alongside a fresh attempt's own feedback.
    const usernameInput = document.querySelector("input[autocomplete='username']") as HTMLInputElement;
    const passwordInput = document.querySelector("input[type='password']") as HTMLInputElement;
    const form = usernameInput.closest("form")!;
    await act(async () => {
      usernameInput.value = "deniz";
      usernameInput.dispatchEvent(new Event("input", { bubbles: true }));
      passwordInput.value = "secret";
      passwordInput.dispatchEvent(new Event("input", { bubbles: true }));
      form.dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));
      await new Promise((resolve) => setTimeout(resolve, 0));
    });

    expect(document.body.textContent).not.toContain("Oturumunuz sonlandırıldı");
  });
});
