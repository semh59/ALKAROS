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

async function renderHome({ capabilities = ["orders.create", "payments.take", "reports.view", "catalog.manage"], health = (): boolean => true, } = {}) {
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input);
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal", capabilities });
    if (path.endsWith("/catalog")) return jsonResponse([]);
    if (path.endsWith("/orders/active")) return jsonResponse(null);
    if (path.endsWith("/health/ready")) return health() ? jsonResponse({ status: "ready" }) : jsonResponse({}, 503);
    return jsonResponse({});
  }));
  const { RouterProvider } = await import("../router");
  const { Cashier } = await import("./Cashier");
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  await act(async () => { root!.render(createElement(RouterProvider, null, createElement(Cashier))); });
  for (let i = 0; i < 3; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
}

const tap = (element: Element) => act(async () => { element.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
const userButton = () => document.querySelector(".pos-header button.operator-card") as HTMLButtonElement;
const menuTexts = () => [...document.querySelectorAll(".header-menu a, .header-menu button")].map((item) => item.textContent);

describe("the till header", () => {
  it("carries at most four items", async () => {
    await renderHome();

    const items = [...document.querySelector(".pos-header-actions")!.children];
    expect(items.length).toBeLessThanOrEqual(4);
    expect(items.map((item) => item.getAttribute("aria-label") ?? item.textContent)).toEqual(
      expect.arrayContaining([expect.stringContaining("Bekleyen hesaplar"), expect.stringContaining("Deniz Kaya")]),
    );
    expect([...document.querySelectorAll(".pos-header-actions > nav a")].map((link) => link.textContent)).toEqual(["Masalar", "Hızlı satış"]);
  });

  it("keeps the rarely used actions in a menu that is closed until the user opens it", async () => {
    await renderHome();

    expect(document.querySelector(".header-menu")).toBeNull();
    expect(userButton().getAttribute("aria-expanded")).toBe("false");
    await tap(userButton());

    expect(userButton().getAttribute("aria-expanded")).toBe("true");
    expect(menuTexts()).toEqual(expect.arrayContaining([
      "Mutfak", "Müşteri ekranı", "Cari hesaplar", "Müşteri ekranını eşleştir", "Ürün menüsü", "Yönetim", "Çıkış",
    ]));
  });

  it("offers only the actions the session may use", async () => {
    await renderHome({ capabilities: ["orders.create"] });
    await tap(userButton());

    expect(menuTexts()).not.toEqual(expect.arrayContaining(["Cari hesaplar"]));
    expect(menuTexts()).not.toEqual(expect.arrayContaining(["Yönetim"]));
    expect(menuTexts()).not.toEqual(expect.arrayContaining(["Ürün menüsü"]));
  });

  it("closes the menu after a choice and on Escape", async () => {
    await renderHome();
    await tap(userButton());
    await tap([...document.querySelectorAll(".header-menu button")].find((item) => item.textContent === "Müşteri ekranı")!);
    expect(document.querySelector(".header-menu")).toBeNull();

    await tap(userButton());
    await act(async () => { window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape" })); });
    expect(document.querySelector(".header-menu")).toBeNull();
  });

  it("always shows the connection state and raises a warning banner when the server stops answering", async () => {
    const server = { up: true };
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    try {
      await renderHome({ health: () => server.up });
      expect(document.querySelector(".connection-state")?.textContent).toBe("Bağlantı var");
      expect(document.querySelector(".connection-warning")).toBeNull();

      server.up = false;
      await act(async () => { vi.advanceTimersByTime(10_000); });
      await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });

      expect(document.querySelector(".connection-state")?.textContent).toBe("Bağlantı yok");
      expect(document.querySelector(".connection-warning")?.textContent).toContain("Bağlantı sorunu");
    } finally {
      vi.useRealTimers();
    }
  });
});
