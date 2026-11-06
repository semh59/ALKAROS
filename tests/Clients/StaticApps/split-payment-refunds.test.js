import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..");
const pageDir = resolve(repoRoot, "src/Clients/Cashier/wwwroot/payments/split-payment");
const htmlPath = resolve(pageDir, "index.html");
const scriptPath = resolve(pageDir, "split-payment.js");

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const PAYMENT_ID = "33333333-3333-3333-3333-333333333333";
const REQUEST_ID = "44444444-4444-4444-4444-444444444444";

const paidSummary = {
  billId: BILL_ID, payableAmount: 100, allocatedTotal: 100, remainingAmount: 0,
  allocations: [{ allocationId: "a1", paymentId: PAYMENT_ID, amount: 100, paymentStatus: "Approved" }],
  unsettledPayment: null,
};
const unpaidSummary = {
  billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null,
};

function request(status, extra = {}) {
  return {
    requestId: REQUEST_ID, billId: BILL_ID, paymentId: PAYMENT_ID, amount: 100, method: "Cash", reason: "Yanlış tutar",
    status, requestedBy: "u1", requestedAt: "2026-10-13T10:00:00Z", decidedBy: null, decidedAt: null,
    slipNumber: null, billCancelled: false, ...extra,
  };
}

function routes({ summary, requests = [], post = {} }) {
  const base = `/api/v1/terminals/${TERMINAL_ID}/billing`;
  return [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u2" } },
    { test: (url) => url.endsWith("/tenders/"), body: summary },
    { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url, init) => url.endsWith(`${base}/bills/${BILL_ID}/cancel`) && init?.method === "POST", ...(post.cancel ?? { body: {} }) },
    { test: (url, init) => url.endsWith(`${base}/bills/${BILL_ID}/refund-requests`) && init?.method === "POST", ...(post.request ?? { body: request("Requested") }) },
    { test: (url, init) => url.endsWith("/approve") && init?.method === "POST", ...(post.approve ?? { body: request("Completed") }) },
    { test: (url, init) => url.endsWith("/reject") && init?.method === "POST", body: request("Rejected") },
    { test: (url, init) => url.endsWith("/complete") && init?.method === "POST", ...(post.complete ?? { body: request("Completed") }) },
    { test: (url, init) => url.endsWith(`${base}/refund-requests`) && (init?.method ?? "GET") === "GET", body: requests },
  ];
}

async function open(routeSet) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter(routeSet);
  await loadApp(htmlPath, scriptPath);
  return fetchMock;
}

const app = () => document.getElementById("app");

function posted(fetchMock, suffix) {
  const call = fetchMock.mock.calls.find(([url, init]) => url.endsWith(suffix) && init?.method === "POST");
  return call ? { url: call[0], body: call[1].body ? JSON.parse(call[1].body) : null } : null;
}

function clickTwice(selector) {
  document.querySelector(selector).click();
  document.querySelector(selector).click();
}

function reasonKey(selector) {
  if (selector === "#cancel-reason") return "cancel";
  const refund = selector.match(/data-refund-reason="([^"]+)"/);
  if (refund) return "refund:" + refund[1];
  const note = selector.match(/data-refund-note="([^"]+)"/);
  return note ? "note:" + note[1] : null;
}

function type(selector, value) {
  const key = reasonKey(selector);
  if (key && !document.querySelector(selector)) {
    const select = document.querySelector('[data-reason-select="' + key + '"]');
    select.value = "Diğer";
    select.dispatchEvent(new Event("change", { bubbles: true }));
  }
  const input = document.querySelector(selector);
  input.value = value;
  input.dispatchEvent(new Event("input", { bubbles: true }));
}

describe("split-payment.js cancel and refund", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("offers approve and reject only on the requests the server says the caller may decide", async () => {
    await open(routes({
      summary: paidSummary,
      requests: [request("Requested", { requestId: "own", mayDecide: false }), request("Requested", { requestId: "other", mayDecide: true })],
    }));
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="approve"]')).not.toBeNull());

    expect([...document.querySelectorAll('[data-refund-action="approve"]')].map((node) => node.getAttribute("data-request-id"))).toEqual(["other"]);
    expect([...document.querySelectorAll('[data-refund-action="reject"]')].map((node) => node.getAttribute("data-request-id"))).toEqual(["other"]);
    expect(app().textContent).toContain("Başka bir yetkili onaylamalı.");
  });

  it.each([
    [false, false, "Kart çekildi bildirimini siz yaptınız"],
    [true, true, "Onayla: kart çekildi"],
  ])("shows the card-charged approval of the caller's own claim only when the server says it may decide (mayDecide %s)", async (mayDecide, approvable) => {
    const pending = { confirmationId: "c1", slipNumber: "FIS-1", amount: 100, requestedBy: "u2", requestedAt: "2026-10-13T10:00:00Z", mayDecide };
    await open(routes({
      summary: { ...unpaidSummary, unsettledPayment: { paymentId: PAYMENT_ID, status: "Unknown", reason: "x", pendingConfirmation: pending } },
    }));
    await vi.waitFor(() => expect(document.getElementById("pending-confirmation")).not.toBeNull());

    expect(document.getElementById("approve-confirmation") !== null).toBe(approvable);
    expect(document.getElementById("reject-confirmation").textContent).toBe("Bildirimi geri çek");
  });

  it("asks for a refund of an approved payment with the typed reason", async () => {
    const fetchMock = await open(routes({ summary: paidSummary }));
    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Ödendi"));
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="request"]')).not.toBeNull());

    type(`[data-refund-reason="${PAYMENT_ID}"]`, "Müşteri fazla ödedi");
    document.querySelector('[data-refund-action="request"]').click();

    await vi.waitFor(() => expect(posted(fetchMock, "/refund-requests")).not.toBeNull());
    expect(posted(fetchMock, "/refund-requests").body).toEqual({ paymentId: PAYMENT_ID, reason: "Müşteri fazla ödedi" });
    await vi.waitFor(() => expect(app().textContent).toContain("İade isteği gönderildi"));
  });

  it("retries a refund request that hit a server fault under the same idempotency key", async () => {
    const routeSet = routes({ summary: paidSummary, post: { request: { status: 500, body: {} } } });
    const requestUrl = `/api/v1/terminals/${TERMINAL_ID}/billing/bills/${BILL_ID}/refund-requests`;
    const refundRoute = routeSet.find((route) => route.test(requestUrl, { method: "POST" }));
    const fetchMock = await open(routeSet);
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="request"]')).not.toBeNull());
    type(`[data-refund-reason="${PAYMENT_ID}"]`, "Müşteri fazla ödedi");

    document.querySelector('[data-refund-action="request"]').click();
    await vi.waitFor(() => expect(app().textContent).toContain("Sunucu tarafında bir sorun"));
    refundRoute.status = 200;
    refundRoute.body = request("Requested");
    document.querySelector('[data-refund-action="request"]').click();
    await vi.waitFor(() => expect(app().textContent).toContain("İade isteği gönderildi"));

    const keys = fetchMock.mock.calls
      .filter(([url, init]) => url.endsWith("/refund-requests") && init?.method === "POST")
      .map(([, init]) => init.headers["X-Idempotency-Key"]);
    expect(keys).toHaveLength(2);
    expect(keys[0]).toBeTruthy();
    expect(keys[1]).toBe(keys[0]);
  });

  it("cancels an unpaid bill with the reason and shows it as cancelled", async () => {
    const fetchMock = await open(routes({ summary: unpaidSummary }));
    await vi.waitFor(() => expect(document.querySelector('[data-reason-select="cancel"]')).not.toBeNull());

    type("#cancel-reason", "Müşteri vazgeçti");
    clickTwice('[data-refund-action="cancel"]');

    await vi.waitFor(() => expect(app().textContent).toContain("Hesap İptal Edildi"));
    expect(posted(fetchMock, "/cancel").body).toEqual({ reason: "Müşteri vazgeçti" });
  });

  it("offers no cancel for a bill that already has money on it", async () => {
    await open(routes({ summary: { ...paidSummary, remainingAmount: 40, allocatedTotal: 60 } }));
    await vi.waitFor(() => expect(app().textContent).toContain("Tahsilat"));

    expect(document.querySelector('[data-reason-select="cancel"]')).toBeNull();
    expect(document.querySelector('[data-refund-action="cancel"]')).toBeNull();
  });

  it("shows the Turkish server message when the cancel is refused, never a raw status", async () => {
    await open(routes({
      summary: unpaidSummary,
      post: { cancel: { status: 409, body: { error: { code: "BILL_HAS_PAYMENT", message: "Bu hesapta ödeme var; iptal etmeden önce ödeme iade edilmeli." } } } },
    }));
    await vi.waitFor(() => expect(document.querySelector('[data-reason-select="cancel"]')).not.toBeNull());

    type("#cancel-reason", "Deneme");
    clickTwice('[data-refund-action="cancel"]');

    await vi.waitFor(() => expect(app().textContent).toContain("önce ödeme iade edilmeli"));
    expect(app().textContent).not.toContain("409");
  });

  it("lets an approver reject a waiting request with a note", async () => {
    const fetchMock = await open(routes({ summary: paidSummary, requests: [request("Requested")] }));
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="approve"]')).not.toBeNull());
    expect(app().textContent).toContain("Onay bekliyor");

    type(`[data-refund-note="${REQUEST_ID}"]`, "Gerekçe yetersiz");
    document.querySelector('[data-refund-action="reject"]').click();
    await vi.waitFor(() => expect(posted(fetchMock, "/reject")).not.toBeNull());
    expect(posted(fetchMock, "/reject").body).toEqual({ note: "Gerekçe yetersiz" });
  });

  it("lets an approver approve a waiting request", async () => {
    const fetchMock = await open(routes({ summary: paidSummary, requests: [request("Requested")] }));
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="approve"]')).not.toBeNull());

    clickTwice('[data-refund-action="approve"]');

    await vi.waitFor(() => expect(posted(fetchMock, "/approve")).not.toBeNull());
    expect(posted(fetchMock, "/approve").url).toContain(`/refund-requests/${REQUEST_ID}/approve`);
  });

  it("shows the cancelled screen when the approval closed the bill", async () => {
    await open(routes({
      summary: paidSummary, requests: [request("Requested")],
      post: { approve: { body: request("Completed", { billCancelled: true }) } },
    }));
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="approve"]')).not.toBeNull());

    clickTwice('[data-refund-action="approve"]');

    await vi.waitFor(() => expect(app().textContent).toContain("Hesap İptal Edildi"));
  });

  it("offers a refund only for approved payments that have no open request", async () => {
    const refunded = {
      ...paidSummary,
      allocations: [
        { allocationId: "a1", paymentId: PAYMENT_ID, amount: 100, paymentStatus: "Refunded" },
        { allocationId: "a2", paymentId: "55555555-5555-5555-5555-555555555555", amount: 50, paymentStatus: "Approved" },
      ],
    };
    await open(routes({
      summary: refunded,
      requests: [request("Requested", { paymentId: "55555555-5555-5555-5555-555555555555" })],
    }));
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="approve"]')).not.toBeNull());

    expect(document.querySelector('[data-refund-action="request"]')).toBeNull();
  });

  it("lists only the refund requests of this bill", async () => {
    await open(routes({
      summary: paidSummary,
      requests: [request("Requested", { billId: "99999999-9999-9999-9999-999999999999", reason: "Başka hesabın isteği" })],
    }));
    await vi.waitFor(() => expect(app().textContent).toContain("Hesap Ödendi"));
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="request"]')).not.toBeNull());

    expect(app().textContent).not.toContain("Başka hesabın isteği");
  });

  it("completes a card refund with the slip number", async () => {
    const fetchMock = await open(routes({ summary: paidSummary, requests: [request("AwaitingManualRefund", { method: "Card" })] }));
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="complete"]')).not.toBeNull());
    expect(app().textContent).toContain("Elle iade bekliyor");

    type(`[data-refund-slip="${REQUEST_ID}"]`, "POS-4471");
    document.querySelector('[data-refund-action="complete"]').click();

    await vi.waitFor(() => expect(posted(fetchMock, "/complete")).not.toBeNull());
    expect(posted(fetchMock, "/complete").body).toEqual({ slipNumber: "POS-4471", note: null });
  });

  it("completes a transfer refund without a slip field and sends no slip number", async () => {
    const fetchMock = await open(routes({ summary: paidSummary, requests: [request("AwaitingManualRefund", { method: "Eft" })] }));
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="complete"]')).not.toBeNull());

    expect(document.querySelector("[data-refund-slip]")).toBeNull();
    expect(app().textContent).toContain("Havale / EFT");
    expect(app().textContent).toContain("fiş numarası gerekmez");
    document.querySelector('[data-refund-action="complete"]').click();

    await vi.waitFor(() => expect(posted(fetchMock, "/complete")).not.toBeNull());
    expect(posted(fetchMock, "/complete").body).toEqual({ slipNumber: null, note: null });
  });

  it("names every method the server can send and marks a self-approved request", async () => {
    await open(routes({
      summary: paidSummary,
      requests: [
        request("Completed", { requestId: "r-cash", method: "Cash" }),
        request("Completed", { requestId: "r-card", method: "Card" }),
        request("Completed", { requestId: "r-eft", method: "Eft", selfApproved: true }),
        request("Completed", { requestId: "r-acc", method: "Account" }),
      ],
    }));
    await vi.waitFor(() => expect(app().textContent).toContain("Havale / EFT"));

    const text = app().textContent;
    for (const label of ["Nakit", "Kart", "Havale / EFT", "Cari hesap"]) expect(text).toContain(label);
    expect(text.match(/Kendi onayı/g)).toHaveLength(1);
    expect(text).not.toContain("Bilinmiyor");
  });

  it("explains a refused self decision with a fixed sentence, never the server text", async () => {
    await open(routes({
      summary: paidSummary,
      requests: [request("Requested")],
      post: { approve: { status: 403, body: { error: { code: "SELF_DECISION", message: "raw english text" } } } },
    }));
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="approve"]')).not.toBeNull());

    document.querySelector('[data-refund-action="approve"]').click();
    await vi.waitFor(() => expect(document.querySelector('[data-refund-action="approve"]')).not.toBeNull());
    document.querySelector('[data-refund-action="approve"]').click();

    await vi.waitFor(() => expect(app().textContent).toContain("Yönetici kendi talebini onaylayabilir"));
    expect(app().textContent).not.toContain("raw english text");
  });
});
