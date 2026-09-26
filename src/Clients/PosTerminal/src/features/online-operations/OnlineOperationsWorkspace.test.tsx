// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import axe from "axe-core";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { OnlineOperationsWorkspace } from "./OnlineOperationsWorkspace";
import type { OnlineOperationsQueue } from "./onlineOperationsApi";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const queue: OnlineOperationsQueue = {
  orders: [
    { orderId: "qr-1", source: "Qr", status: "PendingConfirmation", orderNumber: "QR-001", tableNumber: "12", displayCode: null, total: 180, itemCount: 2, createdAt: "2026-09-26T10:05:00Z", rowVersion: 3 },
    { orderId: "on-1", source: "Online", status: "Accepted", orderNumber: "YS-9f0c", tableNumber: null, displayCode: "YS-778899", total: 320, itemCount: 3, createdAt: "2026-09-26T10:07:00Z", rowVersion: 4 },
    { orderId: "on-2", source: "Online", status: "SomethingNew", orderNumber: "YS-aa11", tableNumber: null, displayCode: "YS-5", total: 50, itemCount: 1, createdAt: "2026-09-26T10:09:00Z", rowVersion: 1 },
  ],
  problems: [
    { inboxId: "p-1", externalOrderId: "ext-1", providerStatus: "RECEIVED", outcome: "Rejected", reason: "UnmappedSku", attempts: 0, receivedAt: "2026-09-26T09:00:00Z" },
    { inboxId: "p-2", externalOrderId: "ext-2", providerStatus: "RECEIVED", outcome: "Diverged", reason: "OutOfStock", attempts: 0, receivedAt: "2026-09-26T09:10:00Z" },
    { inboxId: "p-3", externalOrderId: "ext-3", providerStatus: "RECEIVED", outcome: "Retrying", reason: "SomeFutureCode", attempts: 2, receivedAt: "2026-09-26T09:20:00Z" },
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
    expect(text).toContain("Yeniden denenen katalog yayını: 2");
    // Unknown server values fall back to Turkish, raw codes never reach the screen.
    expect(text).toContain("Diğer");
    for (const raw of ["PendingConfirmation", "Accepted", "UnmappedSku", "OutOfStock", "SomethingNew", "SomeFutureCode", "Rejected", "Diverged"]) {
      expect(text).not.toContain(raw);
    }
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

  it("has no critical or serious automated accessibility findings, including the open dialogs", async () => {
    fetchMock.mockResolvedValue(ok(queue));
    await render();
    await act(async () => { button("İptal et").click(); });

    const report = await axe.run(document.getElementById("root")!, { rules: { "color-contrast": { enabled: false } } });
    expect([...report.violations, ...report.incomplete].filter((item) => item.impact === "critical" || item.impact === "serious")).toEqual([]);
  });
});
