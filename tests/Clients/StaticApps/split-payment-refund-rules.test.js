import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const PAYMENT_ID = "33333333-3333-3333-3333-333333333333";
const REQUEST_ID = "44444444-4444-4444-4444-444444444444";

const paid = { billId: BILL_ID, payableAmount: 200, allocatedTotal: 200, remainingAmount: 0, allocations: [{ allocationId: "a1", paymentId: PAYMENT_ID, amount: 200, paymentStatus: "Approved" }], unsettledPayment: null };
const unpaid = { billId: BILL_ID, payableAmount: 160, allocatedTotal: 0, remainingAmount: 160, allocations: [], unsettledPayment: null };
const receipt = (tableNumber) => ({
  billId: BILL_ID, billNumber: "B-1", status: "Open", currencyCode: "TRY", openedAt: "2026-10-14T10:00:00Z", closedAt: null, tableNumber,
  items: [{ billItemId: "aaaaaaaa-0000-0000-0000-000000000001", name: "Çay", quantity: 3, unitPrice: 50, amount: 150, refundedQuantity: 0 }],
  adjustments: [], subtotal: 200, payableAmount: 200, taxTotal: 0, payments: [],
});
const request = (extra = {}) => ({
  requestId: REQUEST_ID, billId: BILL_ID, paymentId: PAYMENT_ID, amount: 90, method: "Cash", reason: "İkram", status: "Requested", requestedBy: "u1",
  requestedAt: "2026-10-14T10:00:00Z", decidedBy: null, decidedAt: null, slipNumber: null, billCancelled: false, approvedFromAllowance: false, ...extra,
});

async function open({ summary = paid, tableNumber = "M-03", requests = [] } = {}) {
  const base = `/api/v1/terminals/${TERMINAL_ID}/billing`;
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u2", capabilities: ["payments.refund.approve"] } },
    { test: (url) => url.endsWith("/tenders/"), body: summary },
    { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith(`${base}/refund-allowance`), body: { hasOpenSession: false, allowance: 0, used: 0, remaining: 0 } },
    { test: (url) => url.endsWith(`/bills/${BILL_ID}/receipt`), body: receipt(tableNumber) },
    { test: (url) => url.endsWith(`${base}/refund-requests`), body: requests },
    { test: (url) => url.endsWith("/show-on-display"), status: 204 },
  ]);
  await loadApp(resolve(pageDir, "index.html"), resolve(pageDir, "split-payment.js"));
}

const app = () => document.getElementById("app");
const choose = (selector, value) => {
  const select = document.querySelector(selector);
  select.value = value;
  select.dispatchEvent(new Event("change", { bubbles: true }));
};
const click = (selector) => document.querySelector(selector).click();
const labelOf = (control) => control.closest("label")?.querySelector(".sp-field-label")?.textContent ?? "";
const refundFields = () => [...app().querySelectorAll("[data-reason-select], [data-item-payment], [data-item-stock], [data-item-reason], [data-refund-slip], [data-refund-reason], [data-refund-note], #cancel-reason")];
const expectLabelled = (fields, texts) => {
  expect(fields.map(labelOf)).toEqual(texts);
  expect(fields.every((field) => field.closest("label").querySelectorAll("select, input").length === 1)).toBe(true);
  expect(fields.map((field) => field.getAttribute("aria-label"))).toEqual(texts);
};

describe("iade ve iptal formu follows docs/CASHIER_UI_RULES.md", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("gives every refund and cancel field a visible label that is also its accessible name, in each state of the form", async () => {
    await open();
    await vi.waitFor(() => expect(document.querySelector("[data-item-payment]")).not.toBeNull());
    expectLabelled(refundFields(), ["İade gerekçesi (₺200,00)", "İadenin düşeceği ödeme", "Stoğa geri dönüş", "İade gerekçesi"]);

    document.body.innerHTML = "";
    vi.resetModules();
    await open({ summary: unpaid });
    await vi.waitFor(() => expect(document.querySelector('[data-reason-select="cancel"]')).not.toBeNull());
    expectLabelled(refundFields(), ["İptal gerekçesi"]);

    document.body.innerHTML = "";
    vi.resetModules();
    await open({ requests: [request()] });
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="approve"]')).not.toBeNull());
    expectLabelled(refundFields().filter((field) => field.dataset.refundNote !== undefined || field.dataset.reasonSelect?.startsWith("note:")), ["Ret gerekçesi"]);

    document.body.innerHTML = "";
    vi.resetModules();
    await open({ requests: [request({ status: "AwaitingManualRefund" })] });
    await vi.waitFor(() => expect(document.querySelector("[data-refund-slip]")).not.toBeNull());
    expectLabelled(refundFields().filter((field) => field.dataset.reasonSelect?.startsWith("note:") || field.dataset.refundSlip !== undefined), ["İade fiş numarası", "İade iptal gerekçesi"]);
  });

  it("labels the free-text reason field that opens after choosing Diğer", async () => {
    await open();
    await vi.waitFor(() => expect(document.querySelector('[data-reason-select="items"]')).not.toBeNull());

    choose('[data-reason-select="items"]', "Diğer");

    expect(labelOf(document.querySelector("[data-item-reason]"))).toBe("Gerekçeyi yazın");
  });

  it("names the table and the amount on the button that confirms approving a refund", async () => {
    await open({ requests: [request()] });
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="approve"]')).not.toBeNull());

    click('[data-refund-action="approve"]');

    expect(document.querySelector(".sp-btn-danger").textContent).toBe("Evet, iadeyi onayla · Masa M-03 · ₺90,00");
  });

  it("writes a table name that looks like markup as plain text on the confirm button", async () => {
    await open({ requests: [request()], tableNumber: "M<b>3" });
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="approve"]')).not.toBeNull());

    click('[data-refund-action="approve"]');

    expect(document.querySelector(".sp-btn-danger").textContent).toBe("Evet, iadeyi onayla · Masa M<b>3 · ₺90,00");
    expect(document.querySelector(".sp-btn-danger b")).toBeNull();
  });

  it("names the table and the whole bill on the button that confirms cancelling the bill, and a quick sale as Hızlı satış", async () => {
    await open({ summary: unpaid });
    await vi.waitFor(() => expect(document.querySelector('[data-reason-select="cancel"]')).not.toBeNull());
    choose('[data-reason-select="cancel"]', "İkram");
    click('[data-refund-action="cancel"]');
    expect(document.querySelector(".sp-btn-danger").textContent).toBe("Evet, hesabı iptal et · Masa M-03 · ₺160,00");

    document.body.innerHTML = "";
    vi.resetModules();
    await open({ summary: unpaid, tableNumber: null });
    await vi.waitFor(() => expect(document.querySelector('[data-reason-select="cancel"]')).not.toBeNull());
    choose('[data-reason-select="cancel"]', "İkram");
    click('[data-refund-action="cancel"]');
    expect(document.querySelector(".sp-btn-danger").textContent).toBe("Evet, hesabı iptal et · Hızlı satış · ₺160,00");
  });

  it("names the table and the amount when cancelling an approved refund, and says why that button is closed", async () => {
    await open({ requests: [request({ status: "AwaitingManualRefund" })] });
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="cancel-approved"]')).not.toBeNull());

    expect(labelOf(document.querySelector("[data-refund-slip]"))).toBe("İade fiş numarası");
    const closed = document.querySelector('[data-refund-action="cancel-approved"]');
    expect(closed.disabled).toBe(true);
    expect(closed.nextElementSibling?.textContent).toBe("İptal etmek için gerekçeyi seçin ya da yazın.");

    choose(`[data-reason-select="note:${REQUEST_ID}"]`, "İkram");
    click('[data-refund-action="cancel-approved"]');
    expect(document.querySelector(".sp-btn-danger").textContent).toBe("Evet, onaylı iadeyi iptal et · Masa M-03 · ₺90,00");
  });
});
