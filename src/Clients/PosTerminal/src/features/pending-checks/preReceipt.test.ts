// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { registerSessionExpiredHandler } from "../../api";
import { PreReceiptError, printPreReceipt } from "./preReceipt";

const sheet = {
  orderId: "o-1", printNumber: 2, printedAt: "2026-11-18T10:00:00Z", tableNumber: "5", currencyCode: "TRY",
  lines: [{ name: "Köfte <b>", quantity: 2, unitPrice: 100, amount: 200 }, { name: "Ayran", quantity: 1, unitPrice: 30, amount: 30 }],
  adjustments: [{ type: "Tip", amount: 10, isDeduction: false }, { type: "DiscountAmount", amount: 5, isDeduction: true }, { type: "Mystery", amount: 1, isDeduction: false }],
  total: 235, paidAmount: 100,
};
const reply = (status: number, body: unknown) => new Response(JSON.stringify(body), { status });
const text = () => document.getElementById("pre-receipt-print")!.textContent!;

afterEach(() => {
  document.getElementById("pre-receipt-print")?.remove();
  document.body.classList.remove("pre-receipt-printing");
  registerSessionExpiredHandler(null);
});

describe("printPreReceipt", () => {
  it("posts a fresh key, draws the slip and prints once", async () => {
    const fetcher = vi.fn(async () => reply(200, sheet));
    const print = vi.fn();

    await printPreReceipt("t-1", "o-1", fetcher as typeof fetch, print);

    const [url, init] = fetcher.mock.calls[0] as unknown as [string, RequestInit];
    expect([url, init.method, JSON.parse(String(init.body)).idempotencyKey]).toEqual(["/api/v1/terminals/t-1/orders/o-1/pre-receipt", "POST", expect.stringMatching(/^[0-9a-f-]{36}$/)]);
    expect(print).toHaveBeenCalledTimes(1);
    expect(text()).toContain("Bu belge mali fiş değildir.");
    expect(text()).toContain("Hesap fişi no: 2");
    expect(text()).toContain("Masa: 5");
  });

  it("draws lines as text, adjustments in Turkish, and the paid and remaining amounts", async () => {
    await printPreReceipt("t-1", "o-1", (async () => reply(200, sheet)) as typeof fetch, vi.fn());

    expect(document.querySelector("#pre-receipt-print b")).toBeNull();
    expect(text()).toContain("2 × Köfte <b>");
    for (const expected of ["Bahşiş", "İndirim", "Düzeltme"]) expect(text()).toContain(expected);
    expect(text()).not.toMatch(/Tip|Mystery|DiscountAmount/);
    expect(text()).toMatch(/Ödenen.*100,00/);
    expect(text()).toMatch(/Kalan.*135,00/);
  });

  it("keeps the slip in the page only while it prints, so no other print can pick it up", async () => {
    let during: [boolean, boolean] = [false, false];
    const print = vi.fn(() => { during = [document.body.classList.contains("pre-receipt-printing"), document.getElementById("pre-receipt-print") !== null]; });

    await printPreReceipt("t-1", "o-1", (async () => reply(200, sheet)) as typeof fetch, print);
    window.dispatchEvent(new Event("afterprint"));

    expect(during).toEqual([true, true]);
    expect(document.body.classList.contains("pre-receipt-printing")).toBe(false);
    expect(document.getElementById("pre-receipt-print")).toBeNull();
  });

  it("retries a print the server may have recorded with the same key, and a settled print with a new one", async () => {
    const keys: string[] = [];
    const answer = vi.fn(async (_url: string, init?: RequestInit) => {
      keys.push(JSON.parse(String(init?.body)).idempotencyKey);
      if (keys.length === 1) throw new TypeError("network");
      return reply(200, sheet);
    });

    await expect(printPreReceipt("t-1", "o-9", answer as unknown as typeof fetch, vi.fn())).rejects.toBeInstanceOf(PreReceiptError);
    await printPreReceipt("t-1", "o-9", answer as unknown as typeof fetch, vi.fn());
    await printPreReceipt("t-1", "o-9", answer as unknown as typeof fetch, vi.fn());

    expect(keys[1]).toBe(keys[0]);
    expect(keys[2]).not.toBe(keys[0]);
  });

  it("starts the next print of an order with a new key after the server refused one", async () => {
    const keys: string[] = [];
    const answer = vi.fn(async (_url: string, init?: RequestInit) => {
      keys.push(JSON.parse(String(init?.body)).idempotencyKey);
      return reply(409, { error: { code: "ORDER_NOT_OPEN" } });
    });

    await expect(printPreReceipt("t-1", "o-8", answer as unknown as typeof fetch, vi.fn())).rejects.toBeInstanceOf(PreReceiptError);
    await expect(printPreReceipt("t-1", "o-8", answer as unknown as typeof fetch, vi.fn())).rejects.toBeInstanceOf(PreReceiptError);

    expect(keys[1]).not.toBe(keys[0]);
  });

  it("replaces the previous slip instead of stacking another", async () => {
    const fetcher = (async () => reply(200, sheet)) as typeof fetch;
    await printPreReceipt("t-1", "o-1", fetcher, vi.fn());
    await printPreReceipt("t-1", "o-1", fetcher, vi.fn());

    expect(document.querySelectorAll("#pre-receipt-print")).toHaveLength(1);
  });

  it.each([
    ["BILL_NOT_UNPAID", 409, "Bu hesap ödenmiş; ödenmiş hesabın fişini Hesap bölme ekranından yazdırın."],
    ["BILL_OTHER_TERMINAL", 403, "Bu hesap başka bir kasada açık; yalnızca o kasa veya bir yönetici hesap fişi basabilir."],
    ["SURPRISE", 500, "Hesap fişi hazırlanamadı. Tekrar deneyin."],
  ])("turns %s into a Turkish sentence and prints nothing", async (code, status, message) => {
    const print = vi.fn();

    await expect(printPreReceipt("t-1", "o-1", (async () => reply(status, { error: { code, message: "English" } })) as typeof fetch, print))
      .rejects.toEqual(new PreReceiptError(message));
    expect(print).not.toHaveBeenCalled();
    expect(document.getElementById("pre-receipt-print")).toBeNull();
  });

  it("says the server could not be reached, and a 401 ends the session", async () => {
    const expired = vi.fn();
    registerSessionExpiredHandler(expired);

    await expect(printPreReceipt("t-1", "o-1", (async () => { throw new TypeError("network"); }) as typeof fetch, vi.fn()))
      .rejects.toEqual(new PreReceiptError("Sunucuya ulaşılamadı. Hesap fişi basılamadı."));
    await expect(printPreReceipt("t-1", "o-1", (async () => reply(401, {})) as typeof fetch, vi.fn())).rejects.toBeInstanceOf(PreReceiptError);
    expect(expired).toHaveBeenCalledTimes(1);
  });
});
