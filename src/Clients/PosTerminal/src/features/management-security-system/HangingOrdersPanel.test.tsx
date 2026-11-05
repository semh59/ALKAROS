// @vitest-environment jsdom
import { act } from "react";
import { createRoot, type Root } from "react-dom/client";
import { afterEach, describe, expect, it, vi } from "vitest";
import { HangingOrdersPanel } from "./HangingOrdersPanel";
import { createSecurityClient, type SecurityClient } from "./api";
import { ManagementApiError } from "../management/http";

(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true;

const order = (id: string) => ({ orderId: id, orderNumber: `S-${id}`, status: "Submitted", createdAt: "2026-11-01T10:00:00Z", tableNumber: "4", stockLines: 2, wasteLines: 1, servedLines: 0 });

describe("hanging orders panel", () => {
  let root: Root | null = null;
  const make = (overrides: Partial<SecurityClient> = {}) => ({
    hangingOrders: vi.fn().mockResolvedValue([order("a"), order("b")]),
    clearHanging: vi.fn().mockImplementation(async (dryRun: boolean) => dryRun
      ? { dryRun, orders: [order("a"), order("b")], results: [] }
      : { dryRun, orders: [], results: [{ orderId: "a", orderNumber: "S-a", outcome: "Cleared" }, { orderId: "b", orderNumber: "S-b", outcome: "HasPayment" }] }),
    ...overrides,
  } as unknown as SecurityClient);
  async function render(client: SecurityClient) {
    document.body.innerHTML = '<div id="root"></div>';
    root = createRoot(document.getElementById("root")!);
    await act(async () => root!.render(<HangingOrdersPanel client={client} />));
  }
  const button = (start: string) => [...document.querySelectorAll("button")].find((b) => b.textContent?.startsWith(start))!;
  async function chooseReason() {
    const select = document.querySelector("select")!;
    await act(async () => { select.value = "Müşteri vazgeçti"; select.dispatchEvent(new Event("change", { bubbles: true })); });
  }
  afterEach(async () => { if (root) await act(async () => root!.unmount()); root = null; });

  it("lists the orders with their stock and waste lines and clears nothing before the preview", async () => {
    const client = make();
    await render(client);

    expect(document.body.textContent).toContain("S-a");
    expect(document.body.textContent).toContain("2 kalem stoğa dönecek · 1 kalem fire olacak");
    expect(document.body.textContent).toContain("Gönderildi");
    expect(document.body.textContent).toContain("saattir açık");
    expect(document.body.textContent).not.toContain("Submitted");
    expect(client.clearHanging).not.toHaveBeenCalled();
    expect(document.body.textContent).not.toContain("siparişi temizle");
  });

  it("previews with a dry run, then clears only the previewed ids with a reason and shows each result in Turkish", async () => {
    const client = make();
    await render(client);

    await act(async () => button("Temizlenecekleri göster").click());
    expect(client.clearHanging).toHaveBeenLastCalledWith(true, "", []);
    expect(button("2 siparişi temizle").disabled).toBe(true);
    await chooseReason();
    await act(async () => button("2 siparişi temizle").click());
    expect(client.clearHanging).toHaveBeenCalledTimes(1);
    expect(document.body.textContent).toContain("Emin misiniz?");
    await act(async () => button("Evet, 2 siparişi temizle").click());

    expect(client.clearHanging).toHaveBeenLastCalledWith(false, "Müşteri vazgeçti", ["a", "b"]);
    expect(document.body.textContent).toContain("Temizlendi");
    expect(document.body.textContent).toContain("Ödeme var; önce ödeme iade edilmeli");
    expect(document.body.textContent).toContain("1 sipariş temizlendi, 1 sipariş temizlenemedi");
    expect(document.body.textContent).not.toContain("HasPayment");
    expect(document.body.textContent).toContain("nedeni aşağıda");
  });

  it("says less than an hour for a fresh order and lets the manager back out of the confirmation", async () => {
    const fresh = { ...order("n"), createdAt: new Date().toISOString() };
    const client = make({ hangingOrders: vi.fn().mockResolvedValue([fresh]), clearHanging: vi.fn().mockResolvedValue({ dryRun: true, orders: [fresh], results: [] }) });
    await render(client);
    expect(document.body.textContent).toContain("1 saatten kısa süredir açık");
    await act(async () => button("Temizlenecekleri göster").click());
    await chooseReason();
    await act(async () => button("1 siparişi temizle").click());
    await act(async () => button("Vazgeç").click());

    expect(document.body.textContent).not.toContain("Emin misiniz?");
    expect(client.clearHanging).toHaveBeenCalledTimes(1);
  });

  it("asks again when the reason changes after the first confirmation tap", async () => {
    const client = make();
    await render(client);
    await act(async () => button("Temizlenecekleri göster").click());
    await chooseReason();
    await act(async () => button("2 siparişi temizle").click());
    expect(document.body.textContent).toContain("Emin misiniz?");
    const select = document.querySelector("select")!;
    await act(async () => { select.value = "İkram"; select.dispatchEvent(new Event("change", { bubbles: true })); });

    expect(document.body.textContent).not.toContain("Emin misiniz?");
    expect(client.clearHanging).toHaveBeenCalledTimes(1);
  });

  it("explains every outcome in Turkish, counts the age in whole hours and shows a failed real run", async () => {
    const hoursAgo = new Date(Date.now() - 5.5 * 3_600_000).toISOString();
    const rows = [{ ...order("a"), createdAt: hoursAgo, status: "Preparing" }, { ...order("b"), status: "Mystery" }];
    const client = make({
      hangingOrders: vi.fn().mockResolvedValue(rows),
      clearHanging: vi.fn().mockImplementation(async (dryRun: boolean) => {
        if (!dryRun) return { dryRun, orders: [], results: [{ orderId: "a", orderNumber: "S-a", outcome: "Changed" }, { orderId: "b", orderNumber: "S-b", outcome: "HasServedItems" }, { orderId: "c", orderNumber: null, outcome: "Brand-new" }] };
        return { dryRun, orders: rows, results: [] };
      }),
    });
    await render(client);
    expect(document.body.textContent).toContain("Hazırlanıyor");
    expect(document.body.textContent).toContain("5 saattir açık");
    expect(document.body.textContent).toContain("Bilinmeyen durum");
    await act(async () => button("Temizlenecekleri göster").click());
    await chooseReason();
    await act(async () => button("2 siparişi temizle").click());
    await act(async () => button("Evet, 2 siparişi temizle").click());

    expect(document.body.textContent).toContain("Sipariş bu arada değişti; dokunulmadı");
    expect(document.body.textContent).toContain("Servis edilmiş ürün var; hesabı kapatın");
    expect(document.body.textContent).toContain("Bilinmeyen sonuç");
    expect(document.body.textContent).not.toContain("Brand-new");

    await render(make({ clearHanging: vi.fn().mockImplementation(async (dryRun: boolean) => { if (dryRun) return { dryRun, orders: [order("a")], results: [] }; throw new ManagementApiError(409, "CONCURRENCY_CONFLICT", "Kayıt başka biri tarafından değiştirildi; yenileyip tekrar deneyin."); }) }));
    await act(async () => button("Temizlenecekleri göster").click());
    await chooseReason();
    await act(async () => button("1 siparişi temizle").click());
    await act(async () => button("Evet, 1 siparişi temizle").click());
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("Kayıt başka biri tarafından değiştirildi");
  });

  it("shows an empty message when nothing hangs and an error text when the preview fails", async () => {
    await render(make({ hangingOrders: vi.fn().mockResolvedValue([]) }));
    expect(document.body.textContent).toContain("Temizlenecek asılı sipariş yok.");
    await act(async () => root!.unmount());

    await render(make({ clearHanging: vi.fn().mockRejectedValue(new ManagementApiError(403, "FORBIDDEN", "Bu işlem için yetkiniz yok.")) }));
    await act(async () => button("Temizlenecekleri göster").click());
    expect(document.querySelector('[role="alert"]')?.textContent).toContain("Bu işlem için yetkiniz yok.");
  });
});

describe("hanging orders client", () => {
  const fetcher = () => vi.fn().mockResolvedValue(new Response(JSON.stringify({ dryRun: true, orders: [], results: [] }), { status: 200 }));

  it("previews and clears through the same endpoint, sending the reason and the confirmed ids only on the real run", async () => {
    const preview = fetcher();
    await createSecurityClient(preview).clearHanging(true, "", []);
    const real = fetcher();
    await createSecurityClient(real).clearHanging(false, "Müşteri vazgeçti", ["a", "b"]);

    expect(preview.mock.calls[0][0]).toBe("/api/v1/management/security/orders/clear-hanging?dryRun=true");
    expect(real.mock.calls[0][0]).toBe("/api/v1/management/security/orders/clear-hanging?dryRun=false");
    expect(JSON.parse(real.mock.calls[0][1].body)).toEqual({ reason: "Müşteri vazgeçti", orderIds: ["a", "b"] });
  });
});
