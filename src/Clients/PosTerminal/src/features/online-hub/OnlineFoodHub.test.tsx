// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { OnlineFoodHub } from "./OnlineFoodHub";
import { channelStatusText, onlineHubTabFor, type OnlineChannelHealth, type OnlineHubTab } from "./onlineHubApi";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const channels: OnlineChannelHealth[] = [
  { provider: "trendyol-go", displayName: "Trendyol Go", configured: false, lastEventAt: null, polling: "NotPolled" },
  { provider: "yemeksepeti", displayName: "Yemeksepeti", configured: true, lastEventAt: "2026-09-27T09:05:00Z", polling: "NotPolled" },
];

const emptyQueue = { orders: [], problems: [], retries: { pendingProviderEvents: 0, catalogPublicationsRetrying: 0, availabilityDivergences: 0 } };

describe("OnlineFoodHub", () => {
  let root: Root | null = null;
  const fetchMock = vi.fn();
  const ok = (body: unknown) => ({ ok: true, status: 200, json: async () => body });

  beforeEach(() => {
    document.body.innerHTML = '<main><div id="root"></div></main>';
    fetchMock.mockReset();
    fetchMock.mockImplementation(async (url: string) =>
      url.endsWith("/online-channels") ? ok({ platforms: channels })
        : url.includes("/online-platform-credentials") ? ok({ platforms: [] })
          : ok(emptyQueue));
    vi.stubGlobal("fetch", fetchMock);
  });
  afterEach(() => {
    act(() => root?.unmount());
    root = null;
    vi.unstubAllGlobals();
  });

  const manager = ["orders.create", "integrations.manage"];
  const cashier = ["orders.create"];

  async function render(tab: OnlineHubTab, capabilities: readonly string[], onSelectTab: (tab: OnlineHubTab) => void = () => {}) {
    root = createRoot(document.getElementById("root")!);
    await act(async () => { root!.render(<OnlineFoodHub terminalId="t-1" tab={tab} capabilities={capabilities} onSelectTab={onSelectTab} />); });
  }

  const tabs = () => [...document.querySelectorAll('[role="tab"]')].map((t) => t.textContent);
  const urls = () => fetchMock.mock.calls.map(([url]) => String(url));

  it("shows a cashier the orders and the platform status only, and never asks for the settings", async () => {
    await render("menu", cashier);

    expect(tabs()).toEqual(["Siparişler"]);
    expect(urls().some((url) => url.includes("/online-menu/"))).toBe(false);
    expect(document.querySelector('[role="tab"]')?.getAttribute("aria-selected")).toBe("true");
    const text = document.body.textContent ?? "";
    expect(text).toContain("Trendyol Go");
    expect(text).toContain("Bağlantı bilgileri eksik");
    expect(text).toContain("Son sipariş olayı:");
    expect(text).not.toContain("Ayarlara git");
    expect(urls().some((url) => url.includes("online-platform-credentials"))).toBe(false);
    expect(urls()).toContain("/api/v1/terminals/t-1/online-operations?source=all");
  });

  it("gives a manager the settings tab and a way from a missing connection to it", async () => {
    const onSelectTab = vi.fn();
    await render("orders", manager, onSelectTab);

    expect(tabs()).toEqual(["Siparişler", "Menü", "Ayarlar"]);
    await act(async () => { (document.querySelectorAll('[role="tab"]')[1] as HTMLButtonElement).click(); });
    expect(onSelectTab).toHaveBeenLastCalledWith("menu");
    await act(async () => { (document.querySelectorAll('[role="tab"]')[2] as HTMLButtonElement).click(); });
    expect(onSelectTab).toHaveBeenLastCalledWith("settings");
    const goToSettings = [...document.querySelectorAll("button")].find((b) => b.textContent === "Ayarlara git")!;
    await act(async () => { goToSettings.click(); });
    expect(onSelectTab).toHaveBeenCalledTimes(3);

    act(() => root?.unmount());
    await render("settings", manager, onSelectTab);
    expect(urls().some((url) => url.includes("online-platform-credentials"))).toBe(true);
    expect(document.querySelectorAll('[role="tab"]')[2].getAttribute("aria-selected")).toBe("true");

    const results = await axe.run(document.body, { rules: { "color-contrast": { enabled: false } } });
    expect(results.violations).toEqual([]);
  });

  it("shows the problems to staff who see reports, and lets only a reconciliation manager act on them", async () => {
    fetchMock.mockImplementation(async (url: string) =>
      url.endsWith("/online-channels") ? ok({ platforms: [] }) : url.includes("/online-problems") ? ok([
        { caseId: "c-1", kind: "ProviderEventFailed", provider: "yemeksepeti", externalOrderId: "o-1", amount: 0, severity: "Medium",
          status: "Open", openedAt: "2026-09-27T10:00:00Z", rowVersion: 1, nextAction: "ReprocessProviderEvent", canRetry: true },
      ]) : ok(emptyQueue));
    await render("problems", ["orders.create", "reports.view"]);
    expect(tabs()).toEqual(["Siparişler", "Sorunlar"]);
    expect(document.body.textContent).toContain("Platformdan gelen olay işlenemedi");
    expect([...document.querySelectorAll("button")].some((b) => b.textContent === "Yeniden dene")).toBe(false);
    act(() => root?.unmount());

    await render("problems", ["orders.create", "reconciliation.manage"]);
    expect([...document.querySelectorAll("button")].some((b) => b.textContent === "Yeniden dene")).toBe(true);
    act(() => root?.unmount());

    await render("problems", ["orders.create"]);
    expect(tabs()).toEqual(["Siparişler"]);
    expect(urls().filter((url) => url.includes("/online-problems"))).toHaveLength(2);
  });

  it("says why a platform needs attention in Turkish, never with its raw code", async () => {
    const base = channels[1];
    expect(channelStatusText({ ...base, polling: "Failing" })).toBe("Sipariş çekme hata veriyor, yeniden deneniyor");
    expect(channelStatusText({ ...base, polling: "RateLimited" })).toBe("Platform istek sınırına takıldı, bekleniyor");
    expect(channelStatusText({ ...base, lastEventAt: null })).toBe("Henüz sipariş olayı gelmedi");
    expect(channelStatusText({ ...base, configured: false, polling: "Failing" })).toBe("Bağlantı bilgileri eksik");

    fetchMock.mockImplementation(async (url: string) =>
      url.endsWith("/online-channels") ? ok({ platforms: [{ ...base, polling: "Failing" }] }) : ok(emptyQueue));
    await render("orders", cashier);
    const line = [...document.querySelectorAll(".online-hub__channel")][0];
    expect(line.className).toContain("online-hub__channel--warning");
    expect(document.body.textContent).not.toContain("Failing");
  });

  it("explains a status line that cannot be read", async () => {
    fetchMock.mockImplementation(async (url: string) =>
      url.endsWith("/online-channels") ? { ok: false, status: 500, json: async () => ({}) } : ok(emptyQueue));
    await render("orders", cashier);
    expect(document.querySelector(".online-hub__status [role=alert]")?.textContent).toBe("Platform durumu okunamadı.");
  });

  it("opens the matching tab from the old separate paths", () => {
    expect([onlineHubTabFor("/online"), onlineHubTabFor("/online-operations")]).toEqual(["orders", "orders"]);
    expect([onlineHubTabFor("/online/settings"), onlineHubTabFor("/online-platforms")]).toEqual(["settings", "settings"]);
    expect(onlineHubTabFor("/online/menu")).toBe("menu");
    expect(onlineHubTabFor("/online/problems")).toBe("problems");
    expect(onlineHubTabFor("/tables")).toBeNull();
  });
});
