// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

vi.mock("@microsoft/signalr", () => ({
  LogLevel: { Warning: 2 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() { return { on: () => undefined, onreconnecting: () => undefined, onreconnected: () => undefined, onclose: () => undefined, start: vi.fn(async () => undefined), stop: vi.fn(async () => undefined) }; }
  },
}));

let root: Root | null = null;
afterEach(() => {
  if (root) act(() => root!.unmount());
  root = null;
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

describe("till sign-in screen", () => {
  it("puts the cursor in the username field, the only thing a cashier does first", async () => {
    vi.stubGlobal("fetch", vi.fn(async () => new Response(JSON.stringify({}), { status: 401, headers: { "content-type": "application/json" } })));
    const { Cashier } = await import("./Cashier");
    const { RouterProvider } = await import("../router");
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(createElement(RouterProvider, null, createElement(Cashier))));
    for (let tries = 0; tries < 50 && !document.querySelector('input[autocomplete="username"]'); tries++) await act(async () => { await new Promise((done) => setTimeout(done, 20)); });
    const username = document.querySelector<HTMLInputElement>('input[autocomplete="username"]');
    expect(username).not.toBeNull();
    expect(document.activeElement).toBe(username);
  });
});
