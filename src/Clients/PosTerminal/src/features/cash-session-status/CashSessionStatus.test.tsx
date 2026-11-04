// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { registerSessionExpiredHandler } from "../../api";
import { CashSessionBand, CashSessionLink, useCashSessionState } from "./CashSessionStatus";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const answer = (status: number, body: unknown = {}) => ({ ok: status >= 200 && status < 300, status, json: async () => body });

function Probe({ enabled }: { enabled: boolean }) {
  const cashSession = useCashSessionState("t-1", enabled);
  return cashSession ? <><CashSessionLink status={cashSession} /><CashSessionBand status={cashSession} /></> : <p>kapalı özellik</p>;
}

describe("cash session status", () => {
  let root: Root | null = null;
  const fetchMock = vi.fn();

  const show = async (enabled = true) => {
    root = createRoot(document.getElementById("root")!);
    await act(async () => { root!.render(<Probe enabled={enabled} />); });
  };
  const text = () => document.body.textContent ?? "";

  beforeEach(() => {
    document.body.innerHTML = '<div id="root"></div>';
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });
  afterEach(() => {
    act(() => root?.unmount());
    root = null;
    registerSessionExpiredHandler(null);
    vi.unstubAllGlobals();
  });

  it("reads the active session of this terminal and shows Kasa: Açık without a band", async () => {
    fetchMock.mockResolvedValue(answer(200, { cashSessionId: "s-1" }));
    await show();

    expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/terminals/t-1/cash-sessions/active");
    expect(text()).toContain("Kasa: Açık");
    expect(document.querySelector('[role="status"].cash-session-band')).toBeNull();
  });

  it("a 404 means closed: the button says Kasa: Kapalı and a band offers the way to open it", async () => {
    fetchMock.mockResolvedValue(answer(404));
    await show();

    expect(text()).toContain("Kasa: Kapalı");
    const band = document.querySelector(".cash-session-band")!;
    expect(band.textContent).toContain("Kasa oturumu kapalı");
    expect(band.querySelector("a")!.getAttribute("href")).toBe("/cashier/payments/cash-session/index.html");
    expect(band.querySelector("a")!.textContent).toBe("Kasayı aç");
    expect(document.querySelector<HTMLAnchorElement>("a.cash-session-link")!.getAttribute("href")).toBe("/cashier/payments/cash-session/index.html");
  });

  it.each([500, 503, 429])("a %i is a failed read, never 'closed'", async (status) => {
    fetchMock.mockResolvedValue(answer(status, { error: { message: "raw server text" } }));
    await show();

    expect(text()).toContain("Kasa: Bilinmiyor");
    expect(text()).not.toContain("Kasa: Kapalı");
    expect(text()).not.toContain("Kasa oturumu kapalı");
    expect(text()).not.toContain("raw server text");
    expect(text()).toContain("Kasa durumu okunamadı");
  });

  it("a 403 is said plainly (no permission), with no band and no retry", async () => {
    fetchMock.mockResolvedValue(answer(403));
    await show();

    expect(text()).toContain("Kasa: İzin yok");
    expect(document.querySelector("a.cash-session-link")).toBeNull();
    expect(text()).not.toContain("Kasa: Kapalı");
    expect(document.querySelector(".cash-session-band")).toBeNull();
  });

  it("shows Kasa: Kontrol… until the first answer arrives", async () => {
    fetchMock.mockReturnValue(new Promise(() => undefined));
    await show();

    expect(text()).toContain("Kasa: Kontrol…");
  });

  it("coming back to the tab (focus and visibilitychange together) reads once, and a manual retry is never dropped", async () => {
    fetchMock.mockResolvedValue(answer(500));
    await show();
    fetchMock.mockClear();
    fetchMock.mockResolvedValue(answer(200, {}));

    await act(async () => {
      window.dispatchEvent(new Event("focus"));
      document.dispatchEvent(new Event("visibilitychange"));
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("a network failure is a failed read too, and the retry button reads again", async () => {
    fetchMock.mockRejectedValueOnce(new TypeError("Failed to fetch")).mockResolvedValueOnce(answer(200, {}));
    await show();
    expect(text()).toContain("Kasa: Bilinmiyor");

    const retry = [...document.querySelectorAll("button")].find((button) => button.textContent === "Kasa durumunu yeniden oku")!;
    await act(async () => { retry.click(); });

    expect(text()).toContain("Kasa: Açık");
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("reads again when the cashier comes back to this tab after opening the drawer elsewhere", async () => {
    fetchMock.mockResolvedValueOnce(answer(404)).mockResolvedValueOnce(answer(200, {}));
    await show();
    expect(text()).toContain("Kasa: Kapalı");

    await act(async () => { window.dispatchEvent(new Event("focus")); });

    expect(text()).toContain("Kasa: Açık");
  });

  it("a 401 ends the session through the shared handler and is not reported as closed", async () => {
    const expired = vi.fn();
    registerSessionExpiredHandler(expired);
    fetchMock.mockResolvedValue(answer(401));
    await show();

    expect(expired).toHaveBeenCalledTimes(1);
    expect(text()).not.toContain("Kasa: Kapalı");
  });

  it("does nothing for a session without the cash drawer permission", async () => {
    await show(false);

    expect(fetchMock).not.toHaveBeenCalled();
    expect(text()).toBe("kapalı özellik");
  });

  it.each([404, 500])("has no axe violations in the %i state", async (status) => {
    fetchMock.mockResolvedValue(answer(status));
    await show();

    const result = await axe.run(document.body, { rules: { "color-contrast": { enabled: false }, region: { enabled: false } } });
    expect(result.violations).toEqual([]);
  });
});

describe("the till sales header", () => {
  let tillRoot: Root | null = null;
  afterEach(() => {
    if (tillRoot) act(() => tillRoot!.unmount());
    tillRoot = null;
    document.body.innerHTML = "";
    vi.unstubAllGlobals();
    vi.resetModules();
  });

  async function renderTill(capabilities: string[], drawerAnswer: number) {
    const requested: string[] = [];
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      requested.push(path);
      const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });
      if (path.includes("/auth/session")) return json({ userId: "u", displayName: "Deniz Kaya", terminalId: "terminal", capabilities });
      if (path.endsWith("/catalog")) return json([]);
      if (path.endsWith("/orders/active")) return json(null);
      if (path.endsWith("/cash-sessions/active")) return json({}, drawerAnswer);
      return json({ status: "ready" });
    }));
    const { RouterProvider } = await import("../../router");
    const { Cashier } = await import("../../routes/Cashier");
    document.body.innerHTML = '<div id="root"></div>';
    tillRoot = createRoot(document.getElementById("root")!);
    await act(async () => { tillRoot!.render(<RouterProvider><Cashier /></RouterProvider>); });
    for (let i = 0; i < 3; i++) await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
    return requested;
  }

  it("shows Kasa: Kapalı in the header and the band when the drawer is closed", async () => {
    await renderTill(["orders.create", "cash.drawer"], 404);

    expect(document.querySelector("a.header-action.cash-session-link")?.textContent).toBe("Kasa: Kapalı");
    expect(document.querySelector(".cash-session-band")?.textContent).toContain("Kasayı aç");
  });

  it("does not read or show the drawer for a session without cash.drawer", async () => {
    const requested = await renderTill(["orders.create"], 404);

    expect(document.querySelector(".cash-session-link")).toBeNull();
    expect(requested.some((path) => path.includes("/cash-sessions/"))).toBe(false);
  });
});
