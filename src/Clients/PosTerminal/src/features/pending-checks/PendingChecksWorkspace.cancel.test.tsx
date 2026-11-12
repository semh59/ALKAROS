// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { PendingChecksWorkspace } from "./PendingChecksWorkspace";

vi.mock("../../audioAlerts", () => ({ playNewItemChime: vi.fn() }));
vi.mock("@microsoft/signalr", () => ({
  LogLevel: { Warning: 2 },
  HubConnectionBuilder: class {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() { return { on: () => undefined, onreconnected: () => undefined, onreconnecting: () => undefined, onclose: () => undefined, start: async () => undefined, stop: async () => undefined }; }
  },
}));

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const check = (orderId: string, paidAmount: number) => ({
  orderId, orderNumber: `S-${orderId}`, tableNumber: "5", itemCount: 1, total: 90, createdAt: "2026-09-24T11:32:00Z",
  billId: paidAmount > 0 ? `b-${orderId}` : null, paidAmount, itemPreview: "Çay", addedWithoutKitchenCount: 0, tableId: "tb-5",
});
const ok = (body: unknown) => ({ ok: true, status: 200, json: async () => body });
const refused = (status: number, code: string) => ({ ok: false, status, json: async () => ({ error: { code, message: "raw server text" } }) });

describe("cancelling an order from the till queue", () => {
  let root: Root | null = null;
  const fetchMock = vi.fn();
  const buttons = (label: string) => [...document.querySelectorAll("button")].filter((b) => b.textContent === label);
  const click = async (label: string, index = 0) => { await act(async () => buttons(label)[index].click()); };
  async function chooseReason(value: string) {
    const select = document.querySelector<HTMLSelectElement>('select[aria-label="İptal gerekçesi"]')!;
    await act(async () => {
      select.value = value;
      select.dispatchEvent(new Event("change", { bubbles: true }));
    });
  }
  async function render() {
    root = createRoot(document.getElementById("root")!);
    await act(async () => { root!.render(<PendingChecksWorkspace terminalId="t-1" navigateTo={vi.fn()} canCancel canMerge />); });
  }
  const calls = (suffix: string) => fetchMock.mock.calls.filter(([url]) => String(url).endsWith(suffix));

  beforeEach(() => {
    document.body.innerHTML = '<div id="root"></div>';
    fetchMock.mockReset();
    vi.stubGlobal("fetch", fetchMock);
  });
  afterEach(() => {
    act(() => root?.unmount());
    root = null;
    vi.unstubAllGlobals();
  });

  function route(overrides: Record<string, unknown> = {}) {
    let list = [check("o-1", 0), check("o-2", 40)];
    fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
      const path = String(url);
      if (path.endsWith("/awaiting-payment")) return ok(list);
      if (path.endsWith("/orders/o-1") && init?.method !== "POST") return overrides.read ?? ok({ orderId: "o-1", rowVersion: 7 });
      if (path.endsWith("/orders/o-1/cancel")) {
        if (typeof overrides.cancel === "function") return overrides.cancel();
        if (overrides.cancel) return overrides.cancel;
        list = list.filter((row) => row.orderId !== "o-1");
        return ok({ status: "Cancelled" });
      }
      throw new Error(`unexpected ${path}`);
    });
  }

  it("offers the cancel only on a check nothing was collected on", async () => {
    route();
    await render();

    expect(buttons("Siparişi iptal et")).toHaveLength(1);
    expect(document.querySelectorAll("li")[1].textContent).not.toContain("Siparişi iptal et");
  });

  it("needs a reason and a second tap, then cancels with the current row version and refreshes the list", async () => {
    route();
    await render();

    await click("Siparişi iptal et");
    expect(buttons("İptal et")[0].disabled).toBe(true);
    await chooseReason("Yanlış sipariş");
    await click("İptal et");
    expect(calls("/orders/o-1/cancel")).toHaveLength(0);
    expect(document.body.textContent).toContain("Emin misiniz?");
    await click("Evet, siparişi iptal et");

    const [, init] = calls("/orders/o-1/cancel")[0];
    expect(init.method).toBe("POST");
    const body = JSON.parse(init.body);
    expect(body).toMatchObject({ expectedRowVersion: 7, reason: "Yanlış sipariş" });
    expect(body.idempotencyKey).toBeTruthy();
    expect(calls("/awaiting-payment").length).toBeGreaterThanOrEqual(2);
    expect(document.body.textContent).not.toContain("S-o-1");
  });

  it("lets the cashier back out of the second tap", async () => {
    route();
    await render();

    await click("Siparişi iptal et");
    await chooseReason("Yanlış sipariş");
    await click("İptal et");
    await click("Vazgeç");

    expect(calls("/orders/o-1/cancel")).toHaveLength(0);
    expect(document.body.textContent).not.toContain("Emin misiniz?");
  });

  it.each([
    ["ORDER_HAS_SERVED_ITEMS", 409, "Servis edilmiş ürün var; sipariş iptal edilemez, hesabı kapatın."],
    ["ORDER_HAS_PAYMENT", 409, "Bu siparişin hesabında ödeme var; iptal etmeden önce ödeme iade edilmeli."],
    ["CONCURRENCY_CONFLICT", 409, "Kayıt başka bir işlem tarafından değiştirildi; sayfayı yenileyip tekrar deneyin."],
  ])("answers %s with a fixed sentence, never the server text, and reloads the list", async (code, status, sentence) => {
    route({ cancel: refused(status as number, code as string) });
    await render();
    const loadsBefore = calls("/awaiting-payment").length;

    await click("Siparişi iptal et");
    await chooseReason("Yanlış sipariş");
    await click("İptal et");
    await click("Evet, siparişi iptal et");

    expect(document.body.textContent).toContain(sentence);
    expect(document.body.textContent).not.toContain("raw server text");
    expect(document.body.textContent).not.toContain(code as string);
    expect(calls("/awaiting-payment").length).toBeGreaterThan(loadsBefore);
  });

  it("does not post a cancel when the order cannot be read", async () => {
    route({ read: refused(404, "ORDER_NOT_FOUND") });
    await render();

    await click("Siparişi iptal et");
    await chooseReason("Yanlış sipariş");
    await click("İptal et");
    await click("Evet, siparişi iptal et");

    expect(calls("/orders/o-1/cancel")).toHaveLength(0);
    expect(document.querySelector(".pending-checks__cancel-error")?.textContent).toContain("İşlem tamamlanamadı");
    expect(document.querySelector(".pending-checks__error")).toBeNull();
    expect(buttons("İptal et")[0].disabled).toBe(false);
  });

  it("blocks a second submit while the cancel is running", async () => {
    let finish: (value: unknown) => void = () => undefined;
    route({ cancel: new Promise((resolve) => { finish = resolve; }) });
    await render();

    await click("Siparişi iptal et");
    await chooseReason("Yanlış sipariş");
    await click("İptal et");
    await click("Evet, siparişi iptal et");
    expect(document.body.textContent).toContain("İptal ediliyor…");
    expect(buttons("Evet, siparişi iptal et")).toHaveLength(0);
    expect(buttons("İptal et")).toHaveLength(0);
    await act(async () => finish(ok({})));

    expect(calls("/orders/o-1/cancel")).toHaveLength(1);
  });

  it("keeps the form open after a lost answer and retries with the same idempotency key", async () => {
    let attempt = 0;
    route({ cancel: () => { if (attempt++ === 0) throw new TypeError("offline"); return ok({ status: "Cancelled" }); } });
    await render();

    await click("Siparişi iptal et");
    await chooseReason("Yanlış sipariş");
    await click("İptal et");
    await click("Evet, siparişi iptal et");
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("Sunucuya ulaşılamadı");
    expect(buttons("İptal et")).toHaveLength(1);
    await click("İptal et");
    await click("Evet, siparişi iptal et");

    const keys = calls("/orders/o-1/cancel").map(([, init]) => JSON.parse(init.body).idempotencyKey);
    expect(keys).toHaveLength(2);
    expect(keys[1]).toBe(keys[0]);
  });

  it("closes the form when the check leaves the list so the other cancel buttons work again", async () => {
    vi.useFakeTimers({ toFake: ["setInterval", "clearInterval"] });
    try {
      let list = [check("o-1", 0), check("o-3", 0)];
      fetchMock.mockImplementation(async () => ok(list));
      await render();
      await click("Siparişi iptal et", 0);
      expect(buttons("Siparişi iptal et")[0].disabled).toBe(true);

      list = [check("o-3", 0)];
      await act(async () => { await vi.advanceTimersByTimeAsync(60_000); });

      expect(buttons("Siparişi iptal et")).toHaveLength(1);
      expect(buttons("Siparişi iptal et")[0].disabled).toBe(false);
    } finally {
      vi.useRealTimers();
    }
  });

  it("asks again when the reason changes after the first tap", async () => {
    route();
    await render();

    await click("Siparişi iptal et");
    await chooseReason("Yanlış sipariş");
    await click("İptal et");
    expect(document.body.textContent).toContain("Emin misiniz?");
    await chooseReason("İkram");

    expect(document.body.textContent).not.toContain("Emin misiniz?");
    expect(calls("/orders/o-1/cancel")).toHaveLength(0);
  });

  it("still tells the cashier why when the refusal also took the check out of the cancellable list", async () => {
    let list = [check("o-1", 0)];
    fetchMock.mockImplementation(async (url: string, init?: RequestInit) => {
      const path = String(url);
      if (path.endsWith("/awaiting-payment")) return ok(list);
      if (path.endsWith("/orders/o-1") && init?.method !== "POST") return ok({ orderId: "o-1", rowVersion: 7 });
      if (path.endsWith("/orders/o-1/cancel")) {
        list = [check("o-1", 40)];
        return refused(409, "ORDER_HAS_PAYMENT");
      }
      throw new Error(`unexpected ${path}`);
    });
    await render();

    await click("Siparişi iptal et");
    await chooseReason("Yanlış sipariş");
    await click("İptal et");
    await click("Evet, siparişi iptal et");

    expect(buttons("Siparişi iptal et")).toHaveLength(0);
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("ödeme var");
  });
});
