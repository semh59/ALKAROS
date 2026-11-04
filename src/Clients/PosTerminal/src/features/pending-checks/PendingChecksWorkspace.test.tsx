// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { registerSessionExpiredHandler } from "../../api";
import { PendingChecksWorkspace } from "./PendingChecksWorkspace";

const chime = vi.hoisted(() => vi.fn());
vi.mock("../../audioAlerts", () => ({ playNewItemChime: chime }));

// V1-RMD-287: the live link is faked so a test can fire the hub event directly.
const hubHandlers: Record<string, () => void> = {};
const hubLifecycle: { reconnected?: () => void; reconnecting?: () => void } = {};
const hubStart = vi.fn(async () => undefined);
vi.mock("@microsoft/signalr", () => ({
  LogLevel: { Warning: 2 },
  HubConnectionBuilder: class {
    withUrl(url: string) { hubUrls.push(url); return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      return {
        on: (event: string, handler: () => void) => { hubHandlers[event] = handler; },
        onreconnected: (callback: () => void) => { hubLifecycle.reconnected = callback; },
        onreconnecting: (callback: () => void) => { hubLifecycle.reconnecting = callback; },
        onclose: () => undefined,
        start: hubStart,
        stop: async () => undefined,
      };
    }
  },
}));
const hubUrls: string[] = [];

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const sample = [
  { orderId: "o-1", orderNumber: "S-001", tableNumber: "5", itemCount: 3, total: 540, createdAt: "2026-09-24T11:32:00Z", billId: null, paidAmount: 0, itemPreview: "Köfte, Ayran", tableId: "tb-5" },
  { orderId: "o-2", orderNumber: "S-002", tableNumber: "5", itemCount: 1, total: 90, createdAt: "2026-09-24T12:10:00Z", billId: "b-2", paidAmount: 40, itemPreview: "Çay", tableId: "tb-5" },
];

describe("PendingChecksWorkspace", () => {
  let root: Root | null = null;
  const fetchMock = vi.fn();

  beforeEach(() => {
    document.body.innerHTML = '<div id="root"></div>';
    fetchMock.mockReset();
    hubStart.mockReset();
    chime.mockClear();
    hubStart.mockImplementation(async () => undefined);
    hubUrls.length = 0;
    vi.stubGlobal("fetch", fetchMock);
  });
  afterEach(() => {
    registerSessionExpiredHandler(null);
    act(() => root?.unmount());
    root = null;
    vi.unstubAllGlobals();
  });

  async function render(navigateTo = vi.fn()) {
    root = createRoot(document.getElementById("root")!);
    await act(async () => {
      root!.render(<PendingChecksWorkspace terminalId="t-1" navigateTo={navigateTo} />);
    });
    return navigateTo;
  }

  const ok = (body: unknown) => ({ ok: true, status: 200, json: async () => body });

  it("keeps the newest list when an older read finishes last", async () => {
    let finishOld: (value: unknown) => void = () => undefined;
    fetchMock
      .mockImplementationOnce(() => new Promise((resolve) => { finishOld = resolve; }))
      .mockResolvedValue(ok(sample));
    await render();

    await act(async () => { hubHandlers.PendingChecksChanged?.(); });
    expect(document.querySelectorAll("button.pending-checks__row")).toHaveLength(2);

    await act(async () => { finishOld(ok([])); });

    expect(document.querySelectorAll("button.pending-checks__row")).toHaveLength(2);
  });

  it("tells two checks of the same table apart by number, time, items and remaining amount", async () => {
    fetchMock.mockResolvedValue(ok(sample));
    await render();

    const text = document.body.textContent ?? "";
    expect(text).toContain("S-001");
    expect(text).toContain("S-002");
    expect(text).toContain("Köfte, Ayran");
    expect(text).toContain("Kalan"); // the part-paid one shows what is left
    expect(document.querySelectorAll("button.pending-checks__row")).toHaveLength(2);
  });

  it("opens the bill of a check that has none and goes to collection", async () => {
    fetchMock.mockImplementation(async (url: string, init?: RequestInit) =>
      url.includes("awaiting-payment") ? ok(sample) : url.includes("from-order/o-1") && init?.method === "POST" ? ok({ billId: "b-new", allocations: [] }) : { ok: false, status: 404, json: async () => ({}) });
    const navigateTo = await render();

    await act(async () => {
      (document.querySelector('button[aria-label="Masa 5 · 14:32 hesabını tahsil et"]') as HTMLButtonElement).click();
    });

    expect(navigateTo).toHaveBeenCalledWith("/cashier/payments/split-payment/index.html?billId=b-new");
  });

  it("shows a Turkish sentence, not the raw network error, when the bill cannot be opened", async () => {
    fetchMock.mockImplementation(async (url: string) => {
      if (url.includes("awaiting-payment")) return ok(sample);
      return { ok: true, status: 200, json: async () => { throw new TypeError("Failed to fetch"); } };
    });
    await render();

    await act(async () => {
      (document.querySelector('button[aria-label="Masa 5 · 14:32 hesabını tahsil et"]') as HTMLButtonElement).click();
    });

    expect(document.body.textContent).not.toContain("Failed to fetch");
    expect(document.body.textContent).toContain("Hesap açılamadı. Tekrar deneyin.");
  });

  it("goes straight to collection when the bill already exists, without creating another", async () => {
    fetchMock.mockResolvedValue(ok(sample));
    const navigateTo = await render();

    await act(async () => {
      (document.querySelector('button[aria-label="Masa 5 · 15:10 hesabını tahsil et"]') as HTMLButtonElement).click();
    });

    expect(navigateTo).toHaveBeenCalledWith("/cashier/payments/split-payment/index.html?billId=b-2");
    expect(fetchMock.mock.calls.filter(([url]) => String(url).includes("from-order"))).toHaveLength(0);
  });

  it("says so when nothing is waiting and shows a Turkish message when the list cannot be read", async () => {
    fetchMock.mockResolvedValue(ok([]));
    await render();
    expect(document.body.textContent).toContain("Ödeme bekleyen hesap yok.");

    act(() => root?.unmount());
    fetchMock.mockResolvedValue({ ok: false, status: 403, json: async () => ({}) });
    await render();
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("yetkiniz yok");
  });

  it("offers to send a mistaken check back only while nothing is collected, and refreshes after it", async () => {
    let listed = sample;
    fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
      if (String(url).includes("recall-from-cashier")) {
        expect(JSON.parse(String(init?.body))).toEqual({ tableId: "tb-5" });
        listed = sample.filter((check) => check.orderId !== "o-1");
        return ok({ outcome: "Recalled" });
      }
      return ok(listed);
    });
    await render();

    expect(document.querySelector('button[aria-label="Yanlışlıkla gönderildi: masaya geri gönder, Masa 5 · 14:32"]')).not.toBeNull();
    // The part-paid check has money on it: no way back.
    expect(document.querySelector('button[aria-label="Yanlışlıkla gönderildi: masaya geri gönder, Masa 5 · 15:10"]')).toBeNull();

    await act(async () => {
      (document.querySelector('button[aria-label="Yanlışlıkla gönderildi: masaya geri gönder, Masa 5 · 14:32"]') as HTMLButtonElement).click();
    });

    expect(document.body.textContent).not.toContain("S-001");
    expect(document.body.textContent).toContain("S-002");
  });

  it.each([
    ["TABLE_HAS_OPEN_CHECK", "Bu masada yeni bir hesap açık; önce onu kasaya gönderin."],
    ["CHECK_HAS_PAYMENT", "Bu hesapta tahsilat başlamış; masaya geri alınamaz."],
    ["CHECK_NOT_RECALLABLE", "Bu hesap artık kasa kuyruğunda değil; liste yenilendi."],
    ["SOMETHING_NEW", "Hesap masaya geri gönderilemedi."],
  ])("shows a fixed Turkish sentence for %s when the check cannot go back, never the server's text", async (code, sentence) => {
    fetchMock.mockImplementation(async (url: string) =>
      String(url).includes("recall-from-cashier")
        ? { ok: false, status: 409, json: async () => ({ error: { code, message: "raw server text" } }) }
        : ok(sample));
    await render();

    await act(async () => {
      (document.querySelector('button[aria-label="Yanlışlıkla gönderildi: masaya geri gönder, Masa 5 · 14:32"]') as HTMLButtonElement).click();
    });

    expect(document.querySelector('[role="alert"]')?.textContent).toBe(sentence);
    expect(document.body.textContent).not.toContain("raw server text");
  });

  it("names a row by where it came from and when, with the technical number only as a small extra", async () => {
    fetchMock.mockResolvedValue(ok([sample[0], { ...sample[1], orderId: "o-3", orderNumber: "S-003", tableNumber: "—", tableId: null, createdAt: "2026-09-24T12:30:00Z" }]));
    await render();

    const rows = [...document.querySelectorAll("button.pending-checks__row")];
    expect(rows[0].querySelector(".pending-checks__number")?.textContent).toBe("Masa 5 · 14:32");
    expect(rows[0].querySelector(".pending-checks__meta")?.textContent).toBe("3 kalem · S-001");
    expect(rows[1].querySelector(".pending-checks__number")?.textContent).toBe("Hızlı satış · 15:30");
  });

  it("puts the send-back action inside the check's own card", async () => {
    fetchMock.mockResolvedValue(ok(sample));
    await render();

    const card = document.querySelector("li.pending-checks__card")!;
    expect(card.querySelector("button.pending-checks__row")).not.toBeNull();
    expect(card.querySelector("button.pending-checks__recall")).not.toBeNull();
  });

  it("sends a lost session (401) to the shared sign-in handler", async () => {
    const expired = vi.fn();
    registerSessionExpiredHandler(expired);
    fetchMock.mockResolvedValue({ ok: false, status: 401, json: async () => ({}) });
    await render();

    expect(expired).toHaveBeenCalled();
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("Oturum sona erdi");
  });

  it("sends a lost session met while opening a check for payment to the sign-in handler", async () => {
    const expired = vi.fn();
    registerSessionExpiredHandler(expired);
    fetchMock.mockImplementation(async (url: string) => String(url).includes("/billing/")
      ? { ok: false, status: 401, json: async () => ({}) }
      : ok(sample));
    await render();

    await act(async () => { (document.querySelector("button.pending-checks__row") as HTMLButtonElement).click(); });

    expect(expired).toHaveBeenCalled();
  });

  it("reloads the list the moment the hub says the queue changed, and again after a reconnect", async () => {
    fetchMock.mockResolvedValue(ok([]));
    await render();
    expect(hubUrls[0]).toBe("/hubs/waiter-order-status?terminalId=t-1");
    const awaitingCalls = () => fetchMock.mock.calls.filter(([url]) => String(url).includes("awaiting-payment")).length;
    const before = awaitingCalls();

    fetchMock.mockResolvedValue(ok(sample));
    await act(async () => { hubHandlers.PendingChecksChanged(); });
    expect(awaitingCalls()).toBe(before + 1);
    expect(document.querySelectorAll("button.pending-checks__row")).toHaveLength(2);

    await act(async () => { hubLifecycle.reconnected?.(); });
    expect(awaitingCalls()).toBe(before + 2);
  });

  it("stays quiet for checks already waiting and chimes once for a check it has not seen", async () => {
    fetchMock.mockResolvedValue(ok([sample[0]]));
    await render();
    expect(chime).not.toHaveBeenCalled();

    await act(async () => { hubHandlers.PendingChecksChanged(); });
    expect(chime).not.toHaveBeenCalled();

    fetchMock.mockResolvedValue(ok(sample));
    await act(async () => { hubHandlers.PendingChecksChanged(); });
    expect(chime).toHaveBeenCalledTimes(1);

    await act(async () => { hubHandlers.PendingChecksChanged(); });
    expect(chime).toHaveBeenCalledTimes(1);
  });

  it("says the live update is down while the link is down and drops the notice once it is back", async () => {
    fetchMock.mockResolvedValue(ok(sample));
    hubStart.mockImplementation(async () => { throw new Error("down"); });
    await render();
    expect(document.querySelector(".pending-checks__offline")?.textContent).toContain("Canlı güncelleme yok");

    await act(async () => { hubLifecycle.reconnected?.(); });
    expect(document.querySelector(".pending-checks__offline")).toBeNull();

    await act(async () => { hubLifecycle.reconnecting?.(); });
    expect(document.querySelector(".pending-checks__offline")).not.toBeNull();
  });

  it("marks the list as possibly out of date when a refresh fails, with the time of the last good one", async () => {
    fetchMock.mockResolvedValue(ok(sample));
    await render();
    expect(document.querySelector(".pending-checks__stale")).toBeNull();

    fetchMock.mockResolvedValue({ ok: false, status: 500, json: async () => ({}) });
    await act(async () => { hubHandlers.PendingChecksChanged(); });

    expect(document.querySelector(".pending-checks__stale")?.textContent).toMatch(/eski olabilir: son yenileme \d{2}:\d{2}/);
    expect(document.body.textContent).toContain("S-001");
  });

  // V1-RMD-370 (module-by-module UI audit, 2026-09-27): unlike ~13 other
  // feature workspaces (CatalogWorkspace, BillSplitWorkspace,
  // TableWorkspace, etc.), this file had no axe-core scan at all.
  it("has no critical or serious axe violations", async () => {
    document.documentElement.lang = "tr";
    document.title = "ALKAROS bekleyen hesaplar";
    fetchMock.mockResolvedValue(ok(sample));
    await render();
    const report = await axe.run(document, { rules: { "color-contrast": { enabled: false } } });
    expect(report.violations.filter((violation) => violation.impact === "critical" || violation.impact === "serious")).toEqual([]);
  });
});
