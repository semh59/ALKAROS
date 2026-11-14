// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { registerSessionExpiredHandler } from "../../api";
import { CashSessionBand, CashSessionLink, useCashSessionState } from "./CashSessionStatus";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const mine = { cashSessionId: "s-1", cashierUserId: "u-1" };
const answer = (status: number, body: unknown = {}) => ({ ok: status >= 200 && status < 300, status, json: async () => body });

function Probe({ enabled, userId = "u-1" }: { enabled: boolean; userId?: string }) {
  const cashSession = useCashSessionState("t-1", userId, enabled);
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
    fetchMock.mockResolvedValue(answer(200, mine));
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

  it("an open session of another user says so in the header and in a band that names who may close it", async () => {
    fetchMock.mockResolvedValue(answer(200, { cashSessionId: "s-1", cashierUserId: "u-2" }));
    await show();

    expect(text()).toContain("Kasa: Başka kullanıcıda");
    expect(text()).not.toContain("Kasa: Açık");
    const band = document.querySelector(".cash-session-band")!;
    expect(band.textContent).toContain("Oturumu açan kasiyer ya da bir yönetici kapatmalı");
    expect(band.querySelector("a")!.getAttribute("href")).toBe("/cashier/payments/cash-session/index.html");
    expect(band.querySelector("a")!.textContent).toBe("Kasa oturumunu gör");
    expect(document.querySelector<HTMLAnchorElement>("a.cash-session-link")!.getAttribute("href")).toBe("/cashier/payments/cash-session/index.html");
  });

  it("names the owner of another user's session in the header, read from the owner endpoint", async () => {
    fetchMock.mockImplementation(async (url: string) => url.endsWith("/active/owner")
      ? answer(200, { displayName: "Deneme Kasiyer 6" })
      : answer(200, { cashSessionId: "s-1", cashierUserId: "u-2" }));
    await show();

    expect(fetchMock.mock.calls.map((call) => call[0])).toContain("/api/v1/terminals/t-1/cash-sessions/active/owner");
    expect(document.querySelector("a.cash-session-link")!.textContent).toBe("Kasa: Başka kullanıcıda (Deneme Kasiyer 6)");
  });

  it("keeps the plain header text when the owner's name cannot be read, and never asks for it on the own session", async () => {
    fetchMock.mockImplementation(async (url: string) => url.endsWith("/active/owner") ? answer(404) : answer(200, { cashSessionId: "s-1", cashierUserId: "u-2" }));
    await show();
    expect(document.querySelector("a.cash-session-link")!.textContent).toBe("Kasa: Başka kullanıcıda");

    act(() => root?.unmount());
    fetchMock.mockReset();
    fetchMock.mockResolvedValue(answer(200, mine));
    root = createRoot(document.getElementById("root")!);
    await act(async () => { root!.render(<Probe enabled />); });
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it.each([
    ["a failed request", () => { throw new TypeError("Failed to fetch"); }],
    ["a body that is not JSON", async () => ({ ok: true, status: 200, json: async () => { throw new SyntaxError("bad json"); } })],
    ["an empty name", async () => answer(200, { displayName: "" })],
    ["a name that is not text", async () => answer(200, { displayName: 7 })],
  ])("keeps the plain header text for %s from the owner endpoint", async (_, ownerAnswer) => {
    fetchMock.mockImplementation(async (url: string) => url.endsWith("/active/owner") ? ownerAnswer() : answer(200, { cashSessionId: "s-1", cashierUserId: "u-2" }));
    await show();

    expect(document.querySelector("a.cash-session-link")!.textContent).toBe("Kasa: Başka kullanıcıda");
  });

  it("shows the name of the newest read when an older read's owner answer arrives last", async () => {
    let releaseOlder!: (value: unknown) => void;
    const names = [new Promise((resolve) => { releaseOlder = resolve; }), Promise.resolve(answer(200, { displayName: "Yeni" }))];
    fetchMock.mockImplementation(async (url: string) => url.endsWith("/active/owner") ? names.shift() : answer(200, { cashSessionId: "s-1", cashierUserId: "u-2" }));
    await show();
    await act(async () => { root!.render(<Probe enabled userId="u-3" />); });
    expect(text()).toContain("(Yeni)");

    await act(async () => { releaseOlder(answer(200, { displayName: "Eski" })); });

    expect(text()).toContain("(Yeni)");
    expect(text()).not.toContain("Eski");
  });

  it("drops a name read earlier when the next read cannot name the owner", async () => {
    const owner = vi.fn().mockResolvedValueOnce(answer(200, { displayName: "Deneme Kasiyer 6" })).mockResolvedValue(answer(404));
    fetchMock.mockImplementation(async (url: string) => url.endsWith("/active/owner") ? owner() : answer(200, { cashSessionId: "s-1", cashierUserId: "u-2" }));
    await show();
    expect(text()).toContain("(Deneme Kasiyer 6)");

    await act(async () => { window.dispatchEvent(new Event("focus")); });

    expect(document.querySelector("a.cash-session-link")!.textContent).toBe("Kasa: Başka kullanıcıda");
  });

  it("an answer whose body cannot be read is a failed read, not an open drawer", async () => {
    fetchMock.mockResolvedValue({ ok: true, status: 200, json: async () => { throw new SyntaxError("bad json"); } });
    await show();

    expect(text()).toContain("Kasa: Bilinmiyor");
    expect(text()).not.toContain("Kasa: Açık");
  });

  it("reads again and judges ownership again when the signed-in user changes", async () => {
    fetchMock.mockResolvedValue(answer(200, mine));
    await show();
    expect(text()).toContain("Kasa: Açık");

    await act(async () => { root!.render(<Probe enabled userId="u-2" />); });

    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(text()).toContain("Kasa: Başka kullanıcıda");
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
    fetchMock.mockResolvedValue(answer(200, mine));

    await act(async () => {
      window.dispatchEvent(new Event("focus"));
      document.dispatchEvent(new Event("visibilitychange"));
    });

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("a network failure is a failed read too, and the retry button reads again", async () => {
    fetchMock.mockRejectedValueOnce(new TypeError("Failed to fetch")).mockResolvedValueOnce(answer(200, mine));
    await show();
    expect(text()).toContain("Kasa: Bilinmiyor");

    const retry = [...document.querySelectorAll("button")].find((button) => button.textContent === "Kasa durumunu yeniden oku")!;
    await act(async () => { retry.click(); });

    expect(text()).toContain("Kasa: Açık");
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("reads again when the cashier comes back to this tab after opening the drawer elsewhere", async () => {
    fetchMock.mockResolvedValueOnce(answer(404)).mockResolvedValueOnce(answer(200, mine));
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

  it.each([404, 500, 200])("has no axe violations in the %i state (200 is another user's session)", async (status) => {
    fetchMock.mockResolvedValue(answer(status, status === 200 ? { cashSessionId: "s-1", cashierUserId: "u-2" } : {}));
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

  async function renderTill(capabilities: string[], drawerAnswer: number, drawerBody: unknown = {}) {
    const requested: string[] = [];
    vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL) => {
      const path = String(input);
      requested.push(path);
      const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });
      if (path.includes("/auth/session")) return json({ userId: "u", displayName: "Deniz Kaya", terminalId: "terminal", capabilities });
      if (path.endsWith("/catalog")) return json([]);
      if (path.endsWith("/orders/active")) return json(null);
      if (path.endsWith("/cash-sessions/active")) return json(drawerBody, drawerAnswer);
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

  it.each([
    ["u", "Kasa: Açık", false],
    ["someone-else", "Kasa: Başka kullanıcıda", true],
  ])("judges the drawer against the signed-in user: session of %s shows %s", async (owner, label, banded) => {
    await renderTill(["orders.create", "cash.drawer"], 200, { cashSessionId: "s-1", cashierUserId: owner });

    expect(document.querySelector("a.header-action.cash-session-link")?.textContent).toBe(label);
    expect(document.querySelector(".cash-session-band") !== null).toBe(banded);
  });

  it("does not read or show the drawer for a session without cash.drawer", async () => {
    const requested = await renderTill(["orders.create"], 404);

    expect(document.querySelector(".cash-session-link")).toBeNull();
    expect(requested.some((path) => path.includes("/cash-sessions/"))).toBe(false);
  });
});
