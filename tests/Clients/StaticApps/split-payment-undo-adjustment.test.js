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
const adjustment = (id, adjustmentType, amount, isDeduction) => ({ adjustmentId: id, adjustmentType, calculationType: "FixedAmount", amount, isDeduction, reason: "VOLUNTARY_TIP" });
const summaryOf = (payable) => ({ originalPayableAmount: 100, totalDiscounts: 0, totalFees: 0, totalTips: 0, adjustedPayableAmount: payable });

async function open({ items, undo = { body: {} }, delayMs }) {
  let current = items;
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter([
    {
      test: (url, init) => {
        const matches = url.includes("/adjustments/") && url.endsWith("/undo") && init?.method === "POST";
        if (matches && (undo.status ?? 200) < 300) current = current.filter((item) => !url.includes(`/adjustments/${item.adjustmentId}/`));
        return matches;
      },
      delayMs,
      ...undo,
    },
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", get body() { return { ...unpaid, remainingAmount: 100 + current.length }; } },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "66666666-6666-6666-6666-666666666666" } },
    { test: (url) => url.endsWith("/adjustments"), get body() { return { adjustments: current, summary: summaryOf(100 + current.length) }; } },
    { test: (url, init) => url.endsWith("/refund-requests") && (init?.method ?? "GET") === "GET", body: [] },
    { test: (url) => url.endsWith("/show-on-display"), body: {} },
    { test: (url, init) => url.endsWith("/tip") && init?.method === "POST", delayMs: 80, body: {} },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
  await vi.waitFor(() => expect(document.getElementById("app").textContent).toContain("Uygulanan indirim, bahşiş ve ücretler"));
  return { fetchMock, items: () => current };
}

const app = () => document.getElementById("app");
const undoButtons = () => [...document.querySelectorAll("[data-undo-adjustment]")];
const undoCalls = (fetchMock) => fetchMock.mock.calls.filter(([url, init]) => url.endsWith("/undo") && init?.method === "POST").map(([url]) => url);

describe("taking a wrongly entered discount or tip back off on the payment page", () => {
  beforeEach(() => { document.body.innerHTML = ""; });

  it("offers Geri al on discounts and tips only, never on fees", async () => {
    await open({ items: [
      adjustment("a1", "Tip", 10, false), adjustment("a2", "DiscountPercentage", 5, true), adjustment("a3", "DiscountAmount", 5, true),
      adjustment("a4", "ServiceFee", 5, false), adjustment("a5", "Kuver", 5, false), adjustment("a6", "CustomFee", 5, false),
    ] });

    expect(undoButtons().map((button) => button.dataset.undoAdjustment)).toEqual(["a1", "a2", "a3"]);
    expect(undoButtons().every((button) => button.textContent === "Geri al")).toBe(true);
  });

  it("posts to the undo address of that adjustment and refreshes the list", async () => {
    const { fetchMock } = await open({ items: [adjustment("a1", "Tip", 10, false), adjustment("a2", "DiscountAmount", 5, true)] });

    undoButtons()[0].click();

    await vi.waitFor(() => expect(undoButtons().map((button) => button.dataset.undoAdjustment)).toEqual(["a2"]));
    expect(undoCalls(fetchMock)).toEqual([expect.stringContaining(`/bills/${BILL_ID}/adjustments/a1/undo`)]);
    expect(document.getElementById("amount-draft").value).toBe("101.00");
  });

  it("sends one request for a double tap", async () => {
    const { fetchMock } = await open({ items: [adjustment("a1", "Tip", 10, false)], delayMs: 30 });

    undoButtons()[0].click();
    undoButtons()[0]?.click();

    await vi.waitFor(() => expect(undoButtons()).toHaveLength(0));
    expect(undoCalls(fetchMock)).toHaveLength(1);
  });

  it.each([
    [404, { error: { code: "NOT_FOUND", message: "RAW_MSG_LEAK" } }, "zaten kaldırılmış"],
    [409, { error: { code: "UNSUPPORTED_BILL_STATE", message: "RAW_MSG_LEAK" } }, "Bu hesap durumunda indirim ya da bahşiş geri alınamaz."],
    [409, { error: { code: "ADJUSTMENT_UNDO_BELOW_COLLECTED", message: "RAW_MSG_LEAK" } }, "alınan tutarın altına iniyor"],
    [500, { error: { code: "INTERNAL_ERROR", message: "RAW_MSG_LEAK" } }, "Sunucu tarafında bir sorun oluştu"],
  ])("says what went wrong in Turkish for a %s answer", async (status, body, expected) => {
    await open({ items: [adjustment("a1", "Tip", 10, false)], undo: { status, body } });

    undoButtons()[0].click();

    await vi.waitFor(() => expect(document.querySelector('[role="alert"][data-undo-error]')).not.toBeNull());
    expect(document.querySelector("[data-undo-error]").textContent).toContain(expected);
    expect(document.querySelector("[data-undo-error]").textContent).not.toContain("RAW_MSG_LEAK");
    expect(document.activeElement).toBe(document.querySelector("[data-undo-error]"));
  });

  it("says so when the connection fails and keeps the row", async () => {
    const { fetchMock } = await open({ items: [adjustment("a1", "Tip", 10, false)] });
    const original = fetchMock.getMockImplementation();
    fetchMock.mockImplementation(async (url, init) => {
      if (url.endsWith("/undo")) throw new TypeError("Failed to fetch");
      return original(url, init);
    });

    undoButtons()[0].click();

    await vi.waitFor(() => expect(app().textContent).toContain("Bağlantı kurulamadı. Tekrar deneyin."));
    expect(undoButtons()).toHaveLength(1);
  });

  it("does not call a done undo failed when only the refresh afterwards fails", async () => {
    const { fetchMock } = await open({ items: [adjustment("a1", "Tip", 10, false)] });
    const original = fetchMock.getMockImplementation();
    let undone = false;
    fetchMock.mockImplementation(async (url, init) => {
      if (url.endsWith("/undo")) { undone = true; return original(url, init); }
      if (undone && url.endsWith("/adjustments")) throw new TypeError("Failed to fetch");
      return original(url, init);
    });

    undoButtons()[0].click();

    await vi.waitFor(() => expect(app().textContent).toContain("Geri alındı, ancak liste yenilenemedi."));
    expect(app().textContent).not.toContain("Bağlantı kurulamadı");
  });

  it("keeps the buttons off while a tip is being sent", async () => {
    await open({ items: [adjustment("a1", "DiscountAmount", 5, true)] });
    const amount = document.getElementById("tip-amount");
    amount.value = "10";
    amount.dispatchEvent(new Event("input", { bubbles: true }));

    document.getElementById("submit-tip").click();

    expect(undoButtons()[0].disabled).toBe(true);
    await vi.waitFor(() => expect(undoButtons()[0].disabled).toBe(false));
  });

  it("refreshes the list and leaves the amount alone when the entry was already gone", async () => {
    const { fetchMock } = await open({ items: [adjustment("a1", "Tip", 10, false)], undo: { status: 404, body: { error: { code: "NOT_FOUND", message: "RAW_MSG_LEAK" } } } });
    const listCalls = () => fetchMock.mock.calls.filter(([url]) => url.endsWith("/adjustments")).length;
    const before = listCalls();
    const amountBefore = document.getElementById("amount-draft").value;

    undoButtons()[0].click();

    await vi.waitFor(() => expect(document.querySelector("[data-undo-error]")).not.toBeNull());
    expect(listCalls()).toBe(before + 1);
    expect(document.getElementById("amount-draft").value).toBe(amountBefore);
  });

  it("escapes the id in the undo address", async () => {
    const { fetchMock } = await open({ items: [adjustment("a/1 b", "Tip", 10, false)] });

    undoButtons()[0].click();

    await vi.waitFor(() => expect(undoCalls(fetchMock)).toHaveLength(1));
    expect(undoCalls(fetchMock)[0]).toContain("/adjustments/a%2F1%20b/undo");
  });

  it("tells apart which entry a button takes back", async () => {
    await open({ items: [adjustment("a1", "Tip", 10, false), adjustment("a2", "DiscountAmount", 5, true)] });

    expect(undoButtons().map((button) => button.getAttribute("aria-label"))).toEqual([expect.stringMatching(/^Bahşiş .*geri al$/), expect.stringMatching(/^İndirim [(]tutar[)] .*geri al$/)]);
  });
});
