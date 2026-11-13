import { beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const unpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };

function routes(cancel) {
  return [
    { test: (url, init) => url.endsWith("/cancel") && init?.method === "POST", ...cancel },
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: unpaid },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "66666666-6666-6666-6666-666666666666" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url, init) => url.endsWith("/refund-requests") && (init?.method ?? "GET") === "GET", body: [] },
  ];
}

async function open(cancel) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter(routes(cancel));
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.querySelector('[data-reason-select="cancel"]')).not.toBeNull());
  return fetchMock;
}

function chooseReason() {
  const select = document.querySelector('[data-reason-select="cancel"]');
  select.value = "Müşteri vazgeçti";
  select.dispatchEvent(new Event("change", { bubbles: true }));
}

const cancelBody = (fetchMock) => JSON.parse(fetchMock.mock.calls.find(([url, init]) => url.endsWith("/cancel") && init?.method === "POST")[1].body);
const confirmCancel = () => {
  document.querySelector('[data-refund-action="cancel"]').click();
  document.querySelector('[data-refund-action="cancel"]').click();
};
const app = () => document.getElementById("app");

describe("cancelling the order together with the bill on the payment page", () => {
  beforeEach(() => { document.body.innerHTML = ""; });

  it("says why the cancel button is off until a reason is chosen or typed", async () => {
    await open({ body: { orderCancelled: false } });
    const hint = () => document.getElementById("cancel-hint");
    const button = () => document.querySelector('[data-refund-action="cancel"]');

    expect(hint().hidden).toBe(false);
    expect(hint().textContent).toBe("Hesabı iptal etmek için bir gerekçe seçin ya da yazın.");
    expect(button().disabled).toBe(true);
    expect(button().getAttribute("aria-describedby")).toBe("cancel-hint");

    chooseReason();
    expect(hint().hidden).toBe(true);
    expect(button().disabled).toBe(false);
    expect(button().hasAttribute("aria-describedby")).toBe(false);

    const select = document.querySelector('[data-reason-select="cancel"]');
    select.value = "Diğer";
    select.dispatchEvent(new Event("change", { bubbles: true }));
    expect(hint().hidden).toBe(false);
    const typed = document.getElementById("cancel-reason");
    typed.value = "yanlış masa";
    typed.dispatchEvent(new Event("input", { bubbles: true }));
    expect(hint().hidden).toBe(true);
    expect(button().disabled).toBe(false);
    expect(button().hasAttribute("aria-describedby")).toBe(false);

    typed.value = "   ";
    typed.dispatchEvent(new Event("input", { bubbles: true }));
    expect(hint().hidden).toBe(false);
    expect(button().disabled).toBe(true);
    expect(button().getAttribute("aria-describedby")).toBe("cancel-hint");
  });

  it("offers the box unchecked with its explanation and sends no flag when it stays unchecked", async () => {
    const fetchMock = await open({ body: { orderCancelled: false } });

    const box = document.getElementById("cancel-order");
    expect(box.checked).toBe(false);
    expect(app().textContent).toContain("Siparişi de iptal et");
    expect(document.querySelector(".sp-check small").textContent).toBe('İşaretlerseniz sipariş de iptal edilir ve geri alınamaz. Hazırlanmış kalemler fire sayılır, hazırlanmamışların stoğu geri gelir, masa boşalır. İşaretlemezseniz yalnız hesap iptal edilir; sipariş masada kalır, yeniden hesap açabilirsiniz.');
    chooseReason();
    confirmCancel();

    await vi.waitFor(() => expect(app().textContent).toContain("Hesap İptal Edildi"));
    expect(cancelBody(fetchMock)).toEqual({ reason: "Müşteri vazgeçti" });
    expect(app().textContent).not.toContain("masa boşaldı");
  });

  it("sends cancelOrder when the box is checked and says the order was cancelled too", async () => {
    const fetchMock = await open({ body: { orderCancelled: true } });

    chooseReason();
    const box = document.getElementById("cancel-order");
    box.checked = true;
    box.dispatchEvent(new Event("change", { bubbles: true }));
    confirmCancel();

    await vi.waitFor(() => expect(app().textContent).toContain("Hesap ve sipariş iptal edildi; masa boşaldı."));
    expect(cancelBody(fetchMock)).toEqual({ reason: "Müşteri vazgeçti", cancelOrder: true });
  });

  it("asks again when the box changes after the first tap, so the second tap never confirms a different cancellation", async () => {
    const fetchMock = await open({ body: { orderCancelled: true } });

    chooseReason();
    document.querySelector('[data-refund-action="cancel"]').click();
    expect(app().textContent).toContain("Emin misiniz?");
    const box = document.getElementById("cancel-order");
    box.checked = true;
    box.dispatchEvent(new Event("change", { bubbles: true }));

    expect(app().textContent).not.toContain("Emin misiniz?");
    document.querySelector('[data-refund-action="cancel"]').click();
    expect(app().textContent).toContain("Evet, hesabı ve siparişi iptal et");
    expect(fetchMock.mock.calls.some(([url, init]) => url.endsWith("/cancel") && init?.method === "POST")).toBe(false);
  });

  it.each([
    [409, "BILL_HAS_SERVED_ITEMS", 'Bu hesapta servis edilmiş ürün var; kasiyer iptal edemez. Bir yöneticiye başvurun. Ödeme alındıysa "İade iste" ile yönetici onaylı iade başlatabilirsiniz.'],
    [409, "ORDER_HAS_PAYMENT", "Bu siparişin hesabında ödeme var; iptal etmeden önce ödeme iade edilmeli."],
    [409, "ORDER_CANCEL_NOT_ALLOWED", "Sipariş bu durumda iptal edilemez."],
    [409, "ORDER_HAS_SERVED_ITEMS", "Servis edilmiş ürün var; sipariş iptal edilemez, hesabı kapatın."],
    [409, "CONCURRENCY_CONFLICT", "Kayıt başka bir işlem tarafından değiştirildi; sayfayı yenileyip tekrar deneyin."],
    [400, "ORDER_QUANTITY_NOT_ALLOWED", "Bu ürün için bu adet girilemez: tam adet girin."],
    [400, "REFUND_QUANTITY_NOT_WHOLE", "Bu ürün yalnız tam adet olarak iade edilebilir."],
    [400, "REFUND_EARLIER_OVERPAID", "Önceki iade sonradan verilen indirim yüzünden fazla ödendi; bu kalemi diğer kalan kalemlerle birlikte iade edin."],
  ])("answers %i %s with a fixed Turkish sentence, never the server text or the code", async (status, code, sentence) => {
    await open({ status, body: { error: { code, message: "raw server text" } } });

    chooseReason();
    confirmCancel();

    await vi.waitFor(() => expect(app().textContent).toContain(sentence));
    expect(app().textContent).not.toContain("raw server text");
    expect(app().textContent).not.toContain(code);
  });
});
