// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { OnlineOperationsWorkspace } from "./OnlineOperationsWorkspace";
import { loadCustomerNote, type OnlineOperationsQueue } from "./onlineOperationsApi";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const queue: OnlineOperationsQueue = {
  orders: [
    { orderId: "qr-1", source: "Qr", status: "PendingConfirmation", orderNumber: "QR-001", tableNumber: "12", displayCode: null, total: 180, itemCount: 2, createdAt: "2026-09-26T10:05:00Z", rowVersion: 3, provider: null },
    { orderId: "on-1", source: "Online", status: "Accepted", orderNumber: "YS-9f0c", tableNumber: null, displayCode: "YS-778899", total: 320, itemCount: 3, createdAt: "2026-09-26T10:07:00Z", rowVersion: 4, provider: "yemeksepeti" },
    { orderId: "on-2", source: "Online", status: "SomethingNew", orderNumber: "YS-aa11", tableNumber: null, displayCode: "YS-5", total: 50, itemCount: 1, createdAt: "2026-09-26T10:09:00Z", rowVersion: 1, provider: "yemeksepeti" },
  ],
  problems: [
    { inboxId: "p-1", externalOrderId: "ext-1", providerStatus: "RECEIVED", outcome: "Rejected", reason: "UnmappedSku", attempts: 0, receivedAt: "2026-09-26T09:00:00Z", provider: "yemeksepeti" },
    { inboxId: "p-2", externalOrderId: "ext-2", providerStatus: "RECEIVED", outcome: "Diverged", reason: "OutOfStock", attempts: 0, receivedAt: "2026-09-26T09:10:00Z", provider: "yemeksepeti" },
    { inboxId: "p-3", externalOrderId: "ext-3", providerStatus: "RECEIVED", outcome: "Retrying", reason: "SomeFutureCode", attempts: 2, receivedAt: "2026-09-26T09:20:00Z", provider: "yemeksepeti" },
    { inboxId: "p-4", externalOrderId: "ext-4", providerStatus: "RECEIVED", outcome: "Rejected", reason: "UnsupportedItemStatus", attempts: 0, receivedAt: "2026-09-26T09:30:00Z", provider: "yemeksepeti" },
    { inboxId: "p-5", externalOrderId: "ext-5", providerStatus: "PICKED_UP", outcome: "UnknownStatus", reason: null, attempts: 0, receivedAt: "2026-09-26T09:40:00Z", provider: "yemeksepeti" },
  ],
  retries: { pendingProviderEvents: 1, catalogPublicationsRetrying: 2, availabilityDivergences: 0 },
};

describe("OnlineOperationsWorkspace", () => {
  let root: Root | null = null;
  const fetchMock = vi.fn();
  const ok = (body: unknown) => ({ ok: true, status: 200, json: async () => body });

  beforeEach(() => {
    document.body.innerHTML = '<main><div id="root"></div></main>';
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });
  afterEach(() => {
    act(() => root?.unmount());
    root = null;
    vi.unstubAllGlobals();
  });

  async function render() {
    root = createRoot(document.getElementById("root")!);
    await act(async () => { root!.render(<OnlineOperationsWorkspace terminalId="t-1" />); });
  }

  const button = (name: string) =>
    [...document.querySelectorAll("button")].find((b) => b.textContent === name) as HTMLButtonElement;
  const posts = () => fetchMock.mock.calls.filter(([, init]) => (init as RequestInit | undefined)?.method === "POST");

  it("shows both sources, the problems and the retries only in Turkish", async () => {
    fetchMock.mockResolvedValue(ok(queue));
    await render();

    const text = document.body.textContent ?? "";
    expect(text).toContain("Masa 12");
    expect(text).toContain("YS-778899");
    expect(text).toContain("Onay bekliyor");
    expect(text).toContain("Kabul edildi");
    expect(text).toContain("Eşlenmemiş ürün");
    expect(text).toContain("Stok yetersiz");
    expect(text).toContain("Yeniden deneniyor");
    expect(text).toContain("Değiştirilmiş veya desteklenmeyen kalem");
    expect(text).toContain("Yeniden denenen katalog yayını: 2");
    expect(text).toContain("Bilinmeyen sağlayıcı durumu");
    // Unknown server values fall back to Turkish, raw codes never reach the screen.
    expect(text).toContain("Diğer");
    for (const raw of ["PendingConfirmation", "Accepted", "UnmappedSku", "OutOfStock", "SomethingNew", "SomeFutureCode", "Rejected", "Diverged", "UnsupportedItemStatus", "UnknownStatus"]) {
      expect(text).not.toContain(raw);
    }
  });

  it("names each order's and problem's platform and offers no platform filter when there is one", async () => {
    fetchMock.mockResolvedValue(ok(queue));
    await render();

    const text = document.body.textContent ?? "";
    expect(text).toContain("Yemeksepeti");
    expect(text).not.toContain("yemeksepeti");
    expect(document.querySelector('[aria-label="Platforma göre süz"]')).toBeNull();
  });

  it("filters by platform when the queue holds more than one, and never shows a platform id raw", async () => {
    const mixed: OnlineOperationsQueue = {
      ...queue,
      orders: [
        ...queue.orders,
        { orderId: "tg-1", source: "Online", status: "Accepted", orderNumber: "TG-1", tableNumber: null, displayCode: "TG-501", total: 75, itemCount: 1, createdAt: "2026-09-26T10:11:00Z", rowVersion: 2, provider: "trendyol-go" },
        { orderId: "xx-1", source: "Online", status: "Accepted", orderNumber: "XX-1", tableNumber: null, displayCode: "XX-9", total: 10, itemCount: 1, createdAt: "2026-09-26T10:12:00Z", rowVersion: 1, provider: "future-platform" },
      ],
    };
    fetchMock.mockResolvedValue(ok(mixed));
    await render();

    expect(document.querySelector('[aria-label="Platforma göre süz"]')).not.toBeNull();
    expect(document.body.textContent).toContain("Diğer platform");
    expect(document.body.textContent).not.toContain("future-platform");

    await act(async () => { button("Trendyol Go").click(); });

    const text = document.body.textContent ?? "";
    expect(text).toContain("TG-501");
    expect(text).not.toContain("YS-778899");
    expect(text).not.toContain("Masa 12");
    expect(text).toContain("Sorun yok.");
    expect(button("Trendyol Go").getAttribute("aria-pressed")).toBe("true");

    await act(async () => { button("Tüm platformlar").click(); });
    expect(document.body.textContent).toContain("YS-778899");
  });

  it("shows the platform's call number and code with the note only when both are given", async () => {
    fetchMock.mockImplementation(async (url: string) =>
      url.endsWith("/customer-note")
        ? ok(url.includes("on-1") ? { note: "Servis İstiyorum", callPhone: "0212 111 22 33", callCode: "12345678" } : { note: null, callPhone: "0212 111 22 33", callCode: null })
        : ok(queue));
    await render();
    const noteButtons = () => [...document.querySelectorAll("button")].filter((b) => b.textContent === "Müşteri notu");

    await act(async () => { noteButtons()[0].click(); });
    const text = document.body.textContent ?? "";
    expect(text).toContain("Servis İstiyorum");
    expect(text).toContain("0212 111 22 33 numarasını arayın, sonra müşteri arama kodunu tuşlayın: 12345678");

    await act(async () => { noteButtons()[1].click(); });
    expect(document.body.textContent).not.toContain("numarasını arayın");
    expect(document.body.textContent).toContain("Müşteri notu yok.");
  });

  it("drops half of the call information: a number without its code (or the reverse) is never returned", async () => {
    const order = queue.orders[1];
    fetchMock.mockResolvedValueOnce(ok({ note: "n", callPhone: "0212 111 22 33", callCode: null }));
    expect(await loadCustomerNote("t-1", order)).toEqual({ note: "n", callPhone: null, callCode: null });
    fetchMock.mockResolvedValueOnce(ok({ note: null, callPhone: null, callCode: "12345678" }));
    expect(await loadCustomerNote("t-1", order)).toEqual({ note: null, callPhone: null, callCode: null });
    fetchMock.mockResolvedValueOnce(ok({ note: null, callPhone: "0212 111 22 33", callCode: "12345678" }));
    expect(await loadCustomerNote("t-1", order)).toEqual({ note: null, callPhone: "0212 111 22 33", callCode: "12345678" });
  });

  it("never lets an older answer overwrite a newer one", async () => {
    // V12-RMD-006: the first (all-sources) load answers only after the QR filter's load has already answered.
    let releaseFirst: (value: unknown) => void = () => {};
    const qrOnly: OnlineOperationsQueue = { ...queue, orders: [queue.orders[0]], problems: [] };
    fetchMock
      .mockImplementationOnce(() => new Promise((resolve) => { releaseFirst = resolve; }))
      .mockResolvedValue(ok(qrOnly));
    await render();

    await act(async () => { button("QR").click(); });
    await act(async () => { releaseFirst(ok(queue)); });

    const text = document.body.textContent ?? "";
    expect(text).toContain("Masa 12");
    expect(text).not.toContain("YS-778899");
  });

  it("filters by source through the server", async () => {
    fetchMock.mockResolvedValue(ok(queue));
    await render();

    await act(async () => { button("QR").click(); });

    expect(fetchMock.mock.calls.at(-1)?.[0]).toBe("/api/v1/terminals/t-1/online-operations?source=qr");
    expect(button("QR").getAttribute("aria-pressed")).toBe("true");
    expect(button("Tümü").getAttribute("aria-pressed")).toBe("false");
  });

  it("hands an online order over with the version it showed and reloads the persisted result", async () => {
    fetchMock.mockImplementation(async (url: string, init?: RequestInit) =>
      init?.method === "POST" ? ok({ outcome: "Applied" }) : ok(queue));
    await render();
    const loadsBefore = fetchMock.mock.calls.length;

    await act(async () => { button("Kuryeye teslim et").click(); });

    const [url, init] = posts()[0];
    expect(url).toBe("/api/v1/terminals/t-1/online-operations/orders/on-1/hand-over");
    expect(JSON.parse(String((init as RequestInit).body))).toEqual({ expectedRowVersion: 4 });
    expect(fetchMock.mock.calls.length).toBeGreaterThan(loadsBefore + 1);
    expect(document.querySelector('[role="status"]')?.textContent).toBe("YS-778899 siparişi kuryeye teslim edildi.");
  });

  it("explains an action refused because the screen was outdated", async () => {
    fetchMock.mockImplementation(async (_url: string, init?: RequestInit) =>
      init?.method === "POST"
        ? { ok: false, status: 409, json: async () => ({ error: { code: "CONCURRENCY_CONFLICT", message: "Sipariş bu ekran açıldıktan sonra değişti. Listeyi yenileyin." } }) }
        : ok(queue));
    await render();

    await act(async () => { button("Kuryeye teslim et").click(); });

    expect(document.querySelector('[role="alert"]')?.textContent).toBe("Sipariş bu ekran açıldıktan sonra değişti. Listeyi yenileyin.");
    expect(document.querySelector('[role="status"]')?.textContent).toBe("");
  });

  it("rejects a QR order only with a reason, through the existing reject endpoint", async () => {
    fetchMock.mockImplementation(async (_url: string, init?: RequestInit) => (init?.method === "POST" ? ok({}) : ok(queue)));
    await render();

    await act(async () => { button("Reddet").click(); });
    expect(button("Reddi onayla").disabled).toBe(true);
    const input = document.querySelector("input:not([type=radio])") as HTMLInputElement;
    await act(async () => {
      const setter = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!;
      setter.call(input, "Masada kimse yok");
      input.dispatchEvent(new Event("input", { bubbles: true }));
    });
    await act(async () => { button("Reddi onayla").click(); });

    const [url, init] = posts()[0];
    expect(url).toBe("/api/v1/terminals/t-1/orders/qr-1/reject");
    expect(JSON.parse(String((init as RequestInit).body))).toEqual({ expectedRowVersion: 3, reason: "Masada kimse yok" });
  });

  it("cancels an online order with the chosen documented reason", async () => {
    fetchMock.mockImplementation(async (_url: string, init?: RequestInit) => (init?.method === "POST" ? ok({ outcome: "Applied" }) : ok(queue)));
    await render();

    await act(async () => { button("İptal et").click(); });
    const tooBusy = [...document.querySelectorAll("label")].find((l) => l.textContent === "Çok yoğun")!.querySelector("input")!;
    await act(async () => { tooBusy.click(); });
    await act(async () => { button("İptali onayla").click(); });

    const [url, init] = posts()[0];
    expect(url).toBe("/api/v1/terminals/t-1/online-operations/orders/on-1/cancel");
    expect(JSON.parse(String((init as RequestInit).body))).toEqual({ expectedRowVersion: 4, reason: "TooBusy" });
  });

  it("opens the customer note only on request and says so when there is none", async () => {
    fetchMock.mockImplementation(async (url: string) =>
      url.endsWith("/customer-note") ? ok({ note: url.includes("on-1") ? "Zil çalışmıyor" : null }) : ok(queue));
    await render();

    expect(document.body.textContent).not.toContain("Zil çalışmıyor");
    const noteButtons = [...document.querySelectorAll("button")].filter((b) => b.textContent === "Müşteri notu");
    await act(async () => { noteButtons[0].click(); });

    expect(fetchMock.mock.calls.at(-1)?.[0]).toBe("/api/v1/terminals/t-1/online-operations/orders/on-1/customer-note");
    expect(document.body.textContent).toContain("Zil çalışmıyor");

    await act(async () => { noteButtons[1].click(); });
    expect(document.body.textContent).toContain("Müşteri notu yok.");
  });

  it("has no critical or serious automated accessibility findings, including the open dialogs", async () => {
    fetchMock.mockResolvedValue(ok(queue));
    await render();
    await act(async () => { button("İptal et").click(); });

    const report = await axe.run(document.getElementById("root")!, { rules: { "color-contrast": { enabled: false } } });
    expect([...report.violations, ...report.incomplete].filter((item) => item.impact === "critical" || item.impact === "serious")).toEqual([]);
  });
});
