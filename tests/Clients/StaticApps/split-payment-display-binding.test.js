import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";
import { installFetchRouter } from "./support/fetchRouter.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/split-payment");
const htmlPath = resolve(pageDir, "index.html");
const scriptPath = resolve(pageDir, "split-payment.js");

const TERMINAL_ID = "11111111-1111-1111-1111-111111111111";
const BILL_ID = "22222222-2222-2222-2222-222222222222";
const summary = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };

function routes(showOnDisplay) {
  return [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u2" } },
    ...(showOnDisplay ? [{ test: (url) => url.endsWith("/show-on-display"), ...showOnDisplay }] : []),
    { test: (url) => url.endsWith("/tenders/"), body: summary },
    { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
  ];
}

async function open(showOnDisplay) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter(routes(showOnDisplay));
  await loadApp(htmlPath, scriptPath);
  return fetchMock;
}

describe("split-payment.js customer display binding", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("shows the bill being paid on the customer display as soon as the page opens it", async () => {
    const fetchMock = await open({ body: { bound: true } });

    await vi.waitFor(() => {
      const call = fetchMock.mock.calls.find(([url]) => url.endsWith("/show-on-display"));
      expect(call?.[0]).toBe(`/api/v1/terminals/${TERMINAL_ID}/billing/bills/${BILL_ID}/show-on-display`);
      expect(call?.[1].method).toBe("POST");
    });
  });

  it("logs a refusal from the server and keeps the payment page usable", async () => {
    const error = vi.spyOn(console, "error").mockImplementation(() => undefined);
    await open({ status: 403 });

    await vi.waitFor(() => expect(error).toHaveBeenCalledWith("Müşteri ekranına bağlanamadı:", 403));
    expect(document.body.textContent).toContain("Kalan");
    error.mockRestore();
  });

  it("keeps the payment page usable when the customer display cannot be reached", async () => {
    const error = vi.spyOn(console, "error").mockImplementation(() => undefined);
    await open(null);

    await vi.waitFor(() => expect(error).toHaveBeenCalledWith("Müşteri ekranına bağlanamadı:", expect.any(Error)));
    expect(document.body.textContent).toContain("Kalan");
    error.mockRestore();
  });
});

const linkUnpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };
const linkPaid = { ...linkUnpaid, allocatedTotal: 100, remainingAmount: 0 };
const linkLocked = { ...linkUnpaid, unsettledPayment: { paymentId: "33333333-3333-3333-3333-333333333333", pendingConfirmation: false } };

async function openLinkPage(summary, billId = BILL_ID) {
  window.history.replaceState({}, "", `/?billId=${encodeURIComponent(billId)}`);
  installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: summary },
    { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url) => url.endsWith("/show-on-display"), status: 204 },
  ]);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect(document.querySelector(".sp-title")).not.toBeNull());
}

describe("split-payment.js split-by-item entry", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("offers a link to the item and person split screen for this bill", async () => {
    await openLinkPage(linkUnpaid);

    const link = document.getElementById("split-by-item");
    expect(link).not.toBeNull();
    expect(link.getAttribute("href")).toBe(`/billing?billId=${BILL_ID}`);
    expect(link.textContent).toBe("Ürüne veya kişiye göre böl");
  });

  it("hides the link while a card payment waits for reconciliation, like the rest of the payment form", async () => {
    await openLinkPage(linkLocked);

    expect(document.getElementById("split-by-item")).toBeNull();
  });

  it("hides the link once nothing is left to split", async () => {
    await openLinkPage(linkPaid);

    expect(document.getElementById("split-by-item")).toBeNull();
  });

  it("encodes the bill id in the address", async () => {
    await openLinkPage(linkUnpaid, "a b&c");

    expect(document.getElementById("split-by-item").getAttribute("href")).toBe("/billing?billId=a%20b%26c");
  });
});

describe("split-payment.js card method availability", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  const methods = () => [...document.querySelectorAll(".sp-method-chip")].map((chip) => chip.getAttribute("data-method"));

  it("does not draw the card method when the server reports no card terminal", async () => {
    await openLinkPage({ ...linkUnpaid, cardTerminalConfigured: false });

    expect(methods()).toEqual(["Cash", "Eft", "Account"]);
  });

  it("draws the card method when a card terminal is configured", async () => {
    await openLinkPage({ ...linkUnpaid, cardTerminalConfigured: true });

    expect(methods()).toEqual(["Cash", "BankCard", "Eft", "Account"]);
  });

  it("keeps drawing the card method when an older server sends no terminal flag", async () => {
    await openLinkPage(linkUnpaid);

    expect(methods()).toEqual(["Cash", "BankCard", "Eft", "Account"]);
  });

  it("explains a refused card payment, hides the card method and leaves no card selected", async () => {
    let refused = false;
    window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
    installFetchRouter([
      { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
      { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", get body() { return { ...linkUnpaid, cardTerminalConfigured: !refused }; } },
      { test: (url, init) => url.endsWith("/tenders/") && init?.method === "POST", get status() { refused = true; return 409; }, body: { error: { code: "CARD_TERMINAL_NOT_CONFIGURED", message: "x" } } },
      { test: (url) => url.includes("/cash-sessions/active"), status: 404 },
      { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
      { test: (url) => url.endsWith("/refund-requests"), body: [] },
      { test: (url) => url.endsWith("/show-on-display"), status: 204 },
    ]);
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect(document.querySelector(".sp-title")).not.toBeNull());

    document.querySelector('.sp-method-chip[data-method="BankCard"]').click();
    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(document.body.textContent).toContain("Kart terminali tanımlı değil; kartla tahsilat yapılamaz."));
    expect(methods()).toEqual(["Cash", "Eft", "Account"]);
    expect(document.getElementById("method-hint")).not.toBeNull();
  });

  it.each([["Cash", "Nakit"], ["Card", "Kredi/Banka Kartı"], ["Eft", "EFT/Havale"], ["Account", "Hesaba yaz (cari)"], ["", "Tahsilat"]])(
    "names the %s way on the applied payment line",
    async (method, label) => {
      const paid = { ...summary, allocatedTotal: 40, remainingAmount: 60, allocations: [{ allocationId: "a1", paymentId: "p1", amount: 40, paymentStatus: "Approved", method }] };
      window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
      installFetchRouter(routes({ body: { bound: true } }).map((route) => (route.test("/tenders/") ? { ...route, body: paid } : route)));
      await loadApp(htmlPath, scriptPath);

      await vi.waitFor(() => expect(document.querySelector(".sp-line-method")?.textContent).toContain(label));
    },
  );

  it.each([["Initiated", "Başlatıldı"], ["Pending", "Beklemede"], ["Approved", "Onaylandı"], ["Declined", "Reddedildi"], ["Cancelled", "İptal edildi"], ["Unknown", "Sonucu bilinmiyor"], ["ReconciliationRequired", "Mutabakat bekliyor"], ["Refunded", "İade edildi"], ["PartiallyRefunded", "Kısmen iade edildi"], ["Weird", "Bilinmiyor"]])(
    "says %s as %s on the applied payment line",
    async (paymentStatus, label) => {
      const paid = { ...summary, allocatedTotal: 40, remainingAmount: 60, allocations: [{ allocationId: "a1", paymentId: "p1", amount: 40, paymentStatus, method: "Eft" }] };
      window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
      installFetchRouter(routes({ body: { bound: true } }).map((route) => (route.test("/tenders/") ? { ...route, body: paid } : route)));
      await loadApp(htmlPath, scriptPath);

      await vi.waitFor(() => expect(document.querySelector(".sp-line-status")?.textContent).toBe(label));
      expect(document.querySelector(".sp-line-status").classList.contains("is-approved")).toBe(paymentStatus === "Approved");
    },
  );
});
