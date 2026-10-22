// @vitest-environment jsdom

import { act, createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RouterProvider } from "../router";

(globalThis as typeof globalThis & { IS_REACT_ACT_ENVIRONMENT: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });

// V1-WTR-014: captures whatever handler Cashier registers for
// "HelpRequested" so the test can fire it directly instead of standing up a
// real WebSocket - the same reason no other SignalR usage in this codebase
// is unit-tested at this layer (see Cashier.tsx's own note on this), but
// this feature's one real risk (an event-name or payload-field typo between
// the server's HelpRequestedV1 and this file) is exactly what a mock like
// this catches and a typecheck cannot.
const chime = vi.hoisted(() => vi.fn());
vi.mock("../audioAlerts", () => ({ playNewItemChime: chime }));
const handlers: Record<string, (payload: unknown) => void> = {};
const startMock = vi.fn(async () => undefined);
const stopMock = vi.fn(async () => undefined);
const lifecycle: { reconnecting?: () => void; reconnected?: () => void; closed?: () => void } = {};

vi.mock("@microsoft/signalr", () => ({
  LogLevel: { Warning: 2 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      return {
        on: (event: string, handler: (payload: unknown) => void) => { handlers[event] = handler; },
        onreconnecting: (callback: () => void) => { lifecycle.reconnecting = callback; },
        onreconnected: (callback: () => void) => { lifecycle.reconnected = callback; },
        onclose: (callback: () => void) => { lifecycle.closed = callback; },
        start: startMock,
        stop: stopMock,
      };
    }
  },
}));

let root: Root | null = null;

afterEach(() => {
  chime.mockClear();
  vi.useRealTimers();
  startMock.mockReset();
  startMock.mockImplementation(async () => undefined);
  lifecycle.reconnecting = lifecycle.reconnected = lifecycle.closed = undefined;
  if (root) {
    act(() => root!.unmount());
    root = null;
  }
  document.body.innerHTML = "";
  vi.unstubAllGlobals();
  vi.resetModules();
});

describe("Cashier help alerts", () => {
  it("renders a table's help request the moment the hub broadcasts it", async () => {
    localStorage.setItem("alkaros.terminal-id", "call-terminal");
    const fetchMock = vi.fn(async (input: RequestInfo | URL, _init?: RequestInit) => {
      const path = String(input);
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
      }
      if (path.endsWith("/catalog")) return jsonResponse([]);
      if (path.endsWith("/orders/active")) return jsonResponse(null);
      if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
      return jsonResponse({});
    });
    vi.stubGlobal("fetch", fetchMock);

    const { Cashier } = await import("./Cashier");

    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => {
      root!.render(createElement(RouterProvider, null, createElement(Cashier)));
    });
    // Flush the microtask queue so restoreSession's awaited fetches settle
    // and the SignalR effect (gated on session === "ready") actually runs.
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
    await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });

    expect(startMock).toHaveBeenCalled();
    expect(handlers.HelpRequested).toBeDefined();

    await act(async () => {
      handlers.HelpRequested({
        helpRequestId: "call-1",
        tableId: "table-5",
        tableNumber: "T-5",
        requestType: "Complaint",
        requestedByDisplayName: "Ayşe",
      });
    });

    const alert = document.querySelector(".help-alert");
    expect(alert?.textContent).toContain("T-5 masası");
    expect(alert?.textContent).toContain("Misafir şikayeti");
    expect(alert?.textContent).toContain("Ayşe");

    expect(chime).toHaveBeenCalledTimes(1);

    const seen = alert!.querySelector("button")!;
    expect(seen.textContent).toBe("Gördüm");
    await act(async () => { seen.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
    expect(document.querySelector(".help-alert")).toBeNull();
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/v1/terminals/call-terminal/help-requests/call-1/acknowledge",
      expect.objectContaining({ method: "POST" }),
    );
  });
});

// V1-RMD-286: the connection used to give up for good after five automatic
// retries and swallow a failed first start(); a link that stayed down left the
// cashier without any sign that help calls could no longer arrive.
describe("Cashier help connection resilience", () => {
  const sessionFetch = () => vi.fn(async (input: RequestInfo | URL) => {
    const path = String(input);
    if (path.includes("/auth/session")) {
      return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
    }
    if (path.endsWith("/catalog")) return jsonResponse([]);
    if (path.endsWith("/orders/active")) return jsonResponse(null);
    if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
    return jsonResponse({});
  });

  const tick = (ms: number) => act(async () => { await vi.advanceTimersByTimeAsync(ms); });

  async function renderCashier() {
    vi.useFakeTimers();
    vi.stubGlobal("fetch", sessionFetch());
    // vi.resetModules() in afterEach gives each test a fresh router module, so
    // the provider must come from the same import as Cashier.
    const { Cashier } = await import("./Cashier");
    const { RouterProvider: FreshRouterProvider } = await import("../router");
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => {
      root!.render(createElement(FreshRouterProvider, null, createElement(Cashier)));
    });
    await tick(0);
    await tick(0);
  }

  it("keeps retrying when the first start() fails instead of giving up", async () => {
    startMock.mockRejectedValueOnce(new Error("unreachable")).mockRejectedValueOnce(new Error("unreachable"));
    await renderCashier();
    expect(startMock).toHaveBeenCalledTimes(1);
    await tick(1_000);
    expect(startMock).toHaveBeenCalledTimes(2);
    await tick(2_000);
    expect(startMock).toHaveBeenCalledTimes(3);
    // A session that never became established shows no status at all.
    expect(document.body.textContent).not.toContain("Yardım çağrısı bağlantısı");
  });

  it("tells the cashier when an established link drops and that calls may have been missed after it returns", async () => {
    await renderCashier();
    await tick(5_000);

    await act(async () => { lifecycle.reconnecting?.(); });
    expect(document.body.textContent).toContain("Yardım çağrısı bağlantısı koptu, yeniden bağlanılıyor.");

    await act(async () => { lifecycle.reconnected?.(); });
    expect(document.body.textContent).toContain("gelen yardım çağrıları görülmemiş olabilir");

    const dismiss = document.querySelector(".help-alert button")!;
    await act(async () => { dismiss.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
    expect(document.body.textContent).not.toContain("görülmemiş olabilir");
  });

  it("restarts the connection after it closes and stays quiet for a session the hub refuses", async () => {
    await renderCashier();
    // The hub aborts a cashier-only session right after connecting.
    await act(async () => { lifecycle.closed?.(); });
    expect(document.body.textContent).not.toContain("Yardım çağrısı bağlantısı");
    const before = startMock.mock.calls.length;
    await tick(1_000);
    expect(startMock.mock.calls.length).toBe(before + 1);
    // Backoff was not reset by the short-lived connection: next wait is 2 s.
    await act(async () => { lifecycle.closed?.(); });
    await tick(1_000);
    expect(startMock.mock.calls.length).toBe(before + 1);
    await tick(1_000);
    expect(startMock.mock.calls.length).toBe(before + 2);
  });
});

// A call is stored on the Host; the till reloads the ones nobody answered instead of relying on the live push.
describe("Cashier unanswered help calls", () => {
  const openCall = { helpRequestId: "call-9", tableId: "table-9", tableNumber: "T-9", requestType: "Spill", requestedByDisplayName: "Can" };

  const tick = (ms: number) => act(async () => { await vi.advanceTimersByTimeAsync(ms); });

  async function renderCashier(overrides: (path: string, init?: RequestInit) => Response | undefined) {
    vi.useFakeTimers();
    localStorage.setItem("alkaros.terminal-id", "call-terminal");
    const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const path = String(input);
      const override = overrides(path, init);
      if (override) return override;
      if (path.includes("/auth/session")) {
        return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
      }
      if (path.endsWith("/catalog")) return jsonResponse([]);
      if (path.endsWith("/orders/active")) return jsonResponse(null);
      if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
      return jsonResponse({});
    });
    vi.stubGlobal("fetch", fetchMock);
    const { Cashier } = await import("./Cashier");
    const { RouterProvider: FreshRouterProvider } = await import("../router");
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => {
      root!.render(createElement(FreshRouterProvider, null, createElement(Cashier)));
    });
    await tick(0);
    await tick(0);
    return fetchMock;
  }

  const openList = (path: string) =>
    path.endsWith("/help-requests/open") ? jsonResponse([openCall]) : undefined;

  it("shows a call raised while this till was offline once the link is up, and announces it once", async () => {
    await renderCashier(openList);
    await tick(5_000);

    expect(document.querySelector(".help-alert")?.textContent).toContain("T-9 masası");
    expect(chime).toHaveBeenCalledTimes(1);

    await act(async () => {
      handlers.HelpRequested({ ...openCall });
    });
    expect(document.querySelectorAll(".help-alert")).toHaveLength(1);
    expect(chime).toHaveBeenCalledTimes(1);
  });

  it("clears the link warning when the open list could be reloaded after a reconnect", async () => {
    await renderCashier(openList);
    await tick(5_000);
    await act(async () => { lifecycle.reconnecting?.(); });
    await act(async () => { lifecycle.reconnected?.(); });
    await tick(5_000);

    expect(document.body.textContent).not.toContain("görülmemiş olabilir");
  });

  it("keeps the till signed in when the open list is refused for a cashier-only session", async () => {
    await renderCashier((path) => path.endsWith("/help-requests/open")
      ? jsonResponse({ error: { code: "UNAUTHORIZED", message: "Oturum geçersiz veya süresi dolmuş." } }, 401)
      : undefined);
    await tick(5_000);

    expect(document.body.textContent).not.toContain("Oturumunuz sonlandırıldı");
    expect(document.querySelector(".help-alert")).toBeNull();
  });

  it("shows a neutral label instead of an unknown English call type", async () => {
    await renderCashier(() => undefined);
    await act(async () => {
      handlers.HelpRequested({ ...openCall, helpRequestId: "call-x", requestType: "BrokenGlass" });
    });

    const text = document.querySelector(".help-alert")?.textContent ?? "";
    expect(text).toContain("Yardım çağrısı");
    expect(text).not.toContain("BrokenGlass");
  });

  it("keeps the call on screen when the Host could not record that it was seen", async () => {
    await renderCashier((path, init) => init?.method === "POST" && path.endsWith("/acknowledge")
      ? jsonResponse({ error: { code: "X", message: "Hata" } }, 500)
      : undefined);
    await act(async () => { handlers.HelpRequested({ ...openCall }); });

    const seen = document.querySelector(".help-alert button")!;
    await act(async () => { seen.dispatchEvent(new MouseEvent("click", { bubbles: true })); });

    expect(document.querySelector(".help-alert")).not.toBeNull();
  });
});

// The till's screen-pairing dialog shares this file's SignalR mock (the till opens the help-request hub on sign-in).
type Handler = (path: string, init?: RequestInit) => Response | undefined;

async function openPairingDialog(handler: Handler) {
  localStorage.setItem("alkaros.terminal-id", "pair-terminal");
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input);
    const custom = handler(path, init);
    if (custom) return custom;
    if (path.includes("/auth/session")) return jsonResponse({ userId: "user", displayName: "Deniz Kaya", terminalId: "terminal" });
    if (path.endsWith("/catalog")) return jsonResponse([]);
    if (path.endsWith("/orders/active")) return jsonResponse(null);
    if (path.endsWith("/health/ready")) return jsonResponse({ status: "ready" });
    return jsonResponse({});
  });
  vi.stubGlobal("fetch", fetchMock);
  const { Cashier } = await import("./Cashier");
  const { RouterProvider } = await import("../router");
  document.body.innerHTML = '<div id="root"></div>';
  root = createRoot(document.getElementById("root")!);
  await act(async () => { root!.render(createElement(RouterProvider, null, createElement(Cashier))); });
  await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  const trigger = [...document.querySelectorAll("button")].find((button) => button.textContent === "Ekranı eşleştir")!;
  await act(async () => { trigger.dispatchEvent(new MouseEvent("click", { bubbles: true })); });
  await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
  return fetchMock;
}

const click = (button: Element | undefined) => act(async () => {
  button!.dispatchEvent(new MouseEvent("click", { bubbles: true }));
  await new Promise((resolve) => setTimeout(resolve, 0));
});

const byText = (text: string) => [...document.querySelectorAll(".pairing-dialog button")].filter((button) => button.textContent === text).at(-1);

const typeCode = (value: string) => act(async () => {
  const input = document.querySelector<HTMLInputElement>(".pairing-dialog input")!;
  const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;
  setter.call(input, value);
  input.dispatchEvent(new Event("input", { bubbles: true }));
});

describe("Cashier display pairing dialog", () => {
  it("shows when the paired screen's connection ends, so it is not found out in front of a customer", async () => {
    await openPairingDialog((path) => path.endsWith("/display-sessions/current")
      ? jsonResponse({ paired: true, expiresAt: "2026-12-01T20:00:00Z" })
      : undefined);

    expect(document.querySelector(".pairing-session")?.textContent).toContain("Bağlı ekran var");
    expect(document.querySelector(".pairing-session")?.textContent).toContain("kullanıldıkça uzar");
  });

  it("says so when no screen is paired", async () => {
    await openPairingDialog((path) => path.endsWith("/display-sessions/current")
      ? jsonResponse({ paired: false, expiresAt: null })
      : undefined);

    expect(document.querySelector(".pairing-session")?.textContent).toContain("bağlı müşteri ekranı yok");
  });

  it("shows a refused code inside the dialog and clears the code once it is closed", async () => {
    await openPairingDialog((path, init) => init?.method === "POST" && path.endsWith("/pairings/approve")
      ? jsonResponse({ error: { code: "PAIRING_CODE_INVALID", message: "Eşleştirme kodu yanlış, süresi dolmuş ya da daha önce kullanılmış." } }, 404)
      : undefined);
    await typeCode("ABCD2345");

    await click(byText("Ekranı eşleştir"));

    expect(document.querySelector(".pairing-dialog [role='alert']")?.textContent).toContain("Eşleştirme kodu yanlış");

    await click(byText("Vazgeç"));
    expect(document.querySelector(".pairing-dialog")).toBeNull();
    const trigger = [...document.querySelectorAll("button")].find((button) => button.textContent === "Ekranı eşleştir")!;
    await click(trigger);
    expect(document.querySelector<HTMLInputElement>(".pairing-dialog input")!.value).toBe("");
    expect(document.querySelector(".pairing-dialog [role='alert']")).toBeNull();
  });

  it("asks before removing the screen's access and reports how many screens it ended", async () => {
    const fetchMock = await openPairingDialog((path, init) => init?.method === "POST" && path.endsWith("/display-sessions/revoke")
      ? jsonResponse({ revoked: 2 })
      : undefined);
    const revokeCalls = () => fetchMock.mock.calls.filter(([path]) => String(path).endsWith("/display-sessions/revoke")).length;

    await click(byText("Aktif ekran yetkisini kaldır"));
    expect(revokeCalls()).toBe(0);
    expect(document.querySelector(".pairing-dialog")?.textContent).toContain("Emin misiniz?");

    await click(byText("Vazgeç"));
    expect(revokeCalls()).toBe(0);

    await click(byText("Aktif ekran yetkisini kaldır"));
    await click(byText("Evet, yetkiyi kaldır"));
    expect(revokeCalls()).toBe(1);
    expect(document.body.textContent).toContain("2 ekranın yetkisi kaldırıldı.");
  });
});
