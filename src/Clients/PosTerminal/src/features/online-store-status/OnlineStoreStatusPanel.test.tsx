// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { OnlineStoreStatusPanel } from "./OnlineStoreStatusPanel";
import { deliveryText, platformText, requestedText, type OnlineStoreStatus } from "./onlineStoreStatusApi";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const at = "2026-09-27T12:30:00Z";
const localTime = new Date(at).toLocaleTimeString("tr-TR", { hour: "2-digit", minute: "2-digit" });

const rows: OnlineStoreStatus[] = [
  { provider: "trendyol-go", configured: true, state: "Busy", closedUntil: at, delivery: "Retrying", platformState: "Open", platformClosedUntil: null },
  { provider: "yemeksepeti", configured: true, state: "Open", closedUntil: null, delivery: "Delivered", platformState: "Unknown", platformClosedUntil: null },
  { provider: "migros-yemek", configured: false, state: "Open", closedUntil: null, delivery: "Delivered", platformState: "Unknown", platformClosedUntil: null },
];

describe("OnlineStoreStatusPanel", () => {
  let root: Root | null = null;
  const fetchMock = vi.fn();
  const ok = (body: unknown) => ({ ok: true, status: 200, json: async () => body });

  beforeEach(() => {
    document.body.innerHTML = '<main><div id="root"></div></main>';
    fetchMock.mockReset();
    fetchMock.mockImplementation(async (_url: string, init?: RequestInit) => (init?.method === "PUT" ? ok(rows[1]) : ok(rows)));
    vi.stubGlobal("fetch", fetchMock);
  });
  afterEach(() => {
    act(() => root?.unmount());
    root = null;
    vi.unstubAllGlobals();
  });

  async function render() {
    root = createRoot(document.getElementById("root")!);
    await act(async () => { root!.render(<OnlineStoreStatusPanel terminalId="t 1" />); });
  }

  const group = (name: string) => document.querySelector(`[role="group"][aria-label="${name} için restoran durumu"]`)!;
  const button = (scope: Element, text: string) => [...scope.querySelectorAll("button")].find((b) => b.textContent === text)!;
  const puts = () => fetchMock.mock.calls.filter(([, init]) => (init as RequestInit | undefined)?.method === "PUT") as [string, RequestInit][];

  it("shows each platform in Turkish with what was asked, whether it reached the platform, and what the platform says", async () => {
    await render();
    const text = document.body.textContent ?? "";
    expect(text).toContain("Restoran durumu");
    expect(text).toContain(`Yoğun, saat ${localTime} itibarıyla açılacak`);
    expect(text).toContain("Platforma iletilemedi, yeniden deneniyor");
    expect(text).toContain("Platformda: Açık");
    expect(text).toContain("Platformda: Okunamadı");
    expect(text).toContain("Bağlantı bilgileri eksik");
    for (const raw of ["Busy", "Retrying", "Unknown", "trendyol-go", "yemeksepeti", "migros-yemek"]) expect(text).not.toContain(raw);
    // A platform without its settings offers no switch.
    expect(document.querySelectorAll('[role="group"]')).toHaveLength(2);
    expect(button(group("Yemeksepeti"), "Aç").disabled).toBe(true);
    expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/terminals/t%201/online-store-status/");

    const results = await axe.run(document.body, { rules: { "color-contrast": { enabled: false } } });
    expect(results.violations).toEqual([]);
  });

  it("sends open, busy and closed-for-today to the right platform and reloads", async () => {
    await render();
    await act(async () => { button(group("Yemeksepeti"), "30 dk yoğun").click(); });
    await act(async () => { button(group("Yemeksepeti"), "60 dk yoğun").click(); });
    await act(async () => { button(group("Trendyol Go"), "Aç").click(); });
    await act(async () => { button(group("Trendyol Go"), "Bugün kapat").click(); });
    expect(puts().map(([url, init]) => [url, JSON.parse(init.body as string)])).toEqual([
      ["/api/v1/terminals/t%201/online-store-status/yemeksepeti", { state: "Busy", minutes: 30 }],
      ["/api/v1/terminals/t%201/online-store-status/yemeksepeti", { state: "Busy", minutes: 60 }],
      ["/api/v1/terminals/t%201/online-store-status/trendyol-go", { state: "Open" }],
      ["/api/v1/terminals/t%201/online-store-status/trendyol-go", { state: "ClosedToday" }],
    ]);
    expect(fetchMock.mock.calls.filter(([, init]) => (init as RequestInit | undefined)?.method === undefined)).toHaveLength(5);
  });

  it("shows the server's Turkish refusal and never raw codes", async () => {
    fetchMock.mockImplementation(async (_url: string, init?: RequestInit) => (init?.method === "PUT"
      ? { ok: false, status: 409, json: async () => ({ error: { code: "NOT_CONFIGURED", message: "Bu platformun bağlantı bilgileri eksik; önce Ayarlar'dan girin." } }) }
      : ok(rows)));
    await render();
    await act(async () => { button(group("Trendyol Go"), "Bugün kapat").click(); });
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Bu platformun bağlantı bilgileri eksik; önce Ayarlar'dan girin.");
    expect(document.body.textContent).not.toContain("NOT_CONFIGURED");

    fetchMock.mockImplementation(async () => { throw new TypeError("network down"); });
    await act(async () => { button(group("Trendyol Go"), "Aç").click(); });
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Sunucuya ulaşılamadı. Tekrar deneyin.");
    expect(document.body.textContent).not.toContain("network down");
  });

  it("says a forbidden session plainly", async () => {
    fetchMock.mockImplementation(async () => ({ ok: false, status: 403, json: async () => ({}) }));
    await render();
    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Bu işlem için yetkiniz yok.");
  });

  it("words every state, delivery and platform answer, and an unknown one never shows raw", () => {
    const row = (patch: Partial<OnlineStoreStatus>): OnlineStoreStatus => ({ ...rows[1], ...patch });
    expect(requestedText(row({ state: "ClosedToday" }))).toBe("Bugün kapalı");
    expect(requestedText(row({ state: "ClosedUntil", closedUntil: at }))).toBe(`Kapalı, saat ${localTime} itibarıyla açılacak`);
    expect(requestedText(row({ state: "Weird" as never }))).toBe("Bilinmiyor");
    expect(deliveryText(row({ delivery: "Pending" }))).toBe("Platforma iletiliyor…");
    expect(deliveryText(row({ delivery: "Delivered" }))).toBe("");
    expect(platformText(row({ platformState: "Closed", platformClosedUntil: at }))).toBe(`Platformda: Kapalı (saat ${localTime} itibarıyla açılacak)`);
    expect(platformText(row({ platformState: "Closed" }))).toBe("Platformda: Kapalı");
  });
});
