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
const OTHER_BILL_ID = "33333333-3333-3333-3333-333333333333";

const unpaid = (billId) => ({ billId, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null });
const applied = { status: "Applied", billId: BILL_ID, adjustmentId: "a1", summary: {}, grantId: null };
const pending = (grantId) => ({ status: "Pending", billId: BILL_ID, adjustmentId: null, summary: null, grantId });

/** One page load of the payment screen; `answers` answers the discount and comp requests in order. */
async function open(answers, billId = BILL_ID, session = { terminalId: TERMINAL_ID, userId: "u1" }, status = 200) {
  const queue = [...answers];
  const isRequest = (url, init) => (url.endsWith("/discount") || url.endsWith("/comp")) && init?.method === "POST";
  const fetchMock = installFetchRouter([
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), status, body: session },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: unpaid(billId) },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: isRequest, get status() { return queue[0].status; }, get body() { return queue.shift().body; } },
  ]);
  window.history.replaceState({}, "", `/?billId=${billId}`);
  await loadApp(htmlPath, scriptPath);
  if (status === 200) await waitFor(() => expect(document.getElementById("submit-discount")).not.toBeNull());
  else await new Promise((resolve) => setTimeout(resolve, 50));
  return fetchMock;
}

// The whole suite runs these files in parallel; a slow machine must not turn a late redraw into a failure.
const waitFor = (assertion) => vi.waitFor(assertion, { timeout: 15000 });
const requests = (fetchMock, suffix) => fetchMock.mock.calls
  .filter(([url, init]) => url.endsWith(suffix) && init?.method === "POST")
  .map(([, init]) => JSON.parse(init.body));
const type = (id, value) => {
  const input = document.getElementById(id);
  input.value = value;
  input.dispatchEvent(new Event("input", { bubbles: true }));
};
const text = () => document.getElementById("app").textContent;
const submitDiscount = () => document.getElementById("submit-discount");
const stored = () => Object.entries(window.sessionStorage).filter(([key]) => key.includes("pending_approval"));

async function sendDiscount(note = "") {
  type("discount-value", "20");
  if (note) type("discount-note", note);
  submitDiscount().click();
  await waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
}

async function refresh() {
  vi.resetModules();
  document.body.innerHTML = "";
}

describe("split-payment.js waiting approval after a page refresh", () => {
  beforeEach(() => {
    vi.resetModules();
    window.sessionStorage.clear();
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
    window.sessionStorage.clear();
  });

  it("brings the waiting discount back: form, notice, check button and the approve-here box of the manager", async () => {
    const first = await open([{ status: 202, body: pending("g1") }]);
    await sendDiscount("müşteri şikayeti");
    const key = requests(first, "/discount")[0].IdempotencyKey;

    await refresh();
    const second = await open([{ status: 202, body: pending("g1") }]);

    await waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    expect(document.getElementById("discount-value").value).toBe("20");
    expect(document.getElementById("discount-note").value).toBe("müşteri şikayeti");
    await waitFor(() => expect(submitDiscount().textContent).toBe("Onay durumunu kontrol et"));
    expect(document.getElementById("discount-approve-here")).not.toBeNull();
    expect(requests(second, "/discount").every((body) => body.IdempotencyKey === key && body.Value === 20)).toBe(true);
  });

  it("applies a discount the manager decided while the page was gone, with the same key and no second request", async () => {
    const first = await open([{ status: 202, body: pending("g1") }]);
    await sendDiscount();
    const key = requests(first, "/discount")[0].IdempotencyKey;

    await refresh();
    const second = await open([{ status: 200, body: applied }]);

    await waitFor(() => expect(text()).toContain("İndirim uygulandı"));
    const sent = requests(second, "/discount");
    expect(sent).toHaveLength(1);
    expect(sent[0].IdempotencyKey).toBe(key);
    expect(stored()).toEqual([]);
  });

  it("brings a waiting comp back the same way", async () => {
    await open([{ status: 202, body: pending("c1") }]);
    type("comp-amount", "10");
    document.getElementById("submit-comp").click();
    await waitFor(() => expect(text()).toContain("İkram talebiniz yönetici onayına gönderildi"));

    await refresh();
    await open([{ status: 202, body: pending("c1") }]);

    await waitFor(() => expect(text()).toContain("İkram talebiniz yönetici onayına gönderildi"));
    expect(document.getElementById("comp-amount").value).toBe("10");
    expect(document.getElementById("comp-approve-here")).not.toBeNull();
  });

  it("forgets the request when the form is edited, so a refresh shows an empty form", async () => {
    await open([{ status: 202, body: pending("g1") }]);
    await sendDiscount();

    type("discount-value", "30");
    await refresh();
    await open([]);

    expect(stored()).toEqual([]);
    expect(document.getElementById("discount-value").value).toBe("");
    expect(text()).not.toContain("yönetici onayına gönderildi");
  });

  it("forgets a waiting comp that was edited, so a refresh shows an empty comp form", async () => {
    await open([{ status: 202, body: pending("c1") }]);
    type("comp-amount", "10");
    document.getElementById("submit-comp").click();
    await waitFor(() => expect(text()).toContain("İkram talebiniz yönetici onayına gönderildi"));

    type("comp-amount", "15");
    await refresh();
    await open([]);

    expect(stored()).toEqual([]);
    expect(document.getElementById("comp-amount").value).toBe("");
  });

  it("ignores a record of another cashier on the same tab", async () => {
    await open([{ status: 202, body: pending("g1") }]);
    await sendDiscount();

    await refresh();
    const second = await open([], BILL_ID, { terminalId: TERMINAL_ID, userId: "u2" });

    expect(document.getElementById("discount-value").value).toBe("");
    expect(text()).not.toContain("yönetici onayına gönderildi");
    expect(requests(second, "/discount")).toHaveLength(0);
  });

  it("keeps the record when the screen could not load, so the request is still there after signing in again", async () => {
    await open([{ status: 202, body: pending("g1") }]);
    await sendDiscount();

    await refresh();
    await open([], BILL_ID, {}, 401);
    expect(stored()).toHaveLength(1);

    await refresh();
    await open([{ status: 202, body: pending("g1") }]);
    await waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
  });

  it("ignores a record of another bill", async () => {
    await open([{ status: 202, body: pending("g1") }]);
    await sendDiscount();

    await refresh();
    await open([], OTHER_BILL_ID);

    expect(document.getElementById("discount-value").value).toBe("");
    expect(text()).not.toContain("yönetici onayına gönderildi");
  });

  it("keeps no manager name, password or typed secret in storage", async () => {
    await open([{ status: 202, body: pending("g1") }]);
    await sendDiscount();
    document.getElementById("discount-approver-name").value = "mudur";
    document.getElementById("discount-approver-secret").value = "Gizli-Sifre-99";
    document.getElementById("discount-approve-here").click();
    await waitFor(() => expect(text()).toContain("Bağlantı kurulamadı"));

    const all = JSON.stringify(Object.entries(window.sessionStorage));
    expect(all).not.toContain("Gizli-Sifre-99");
    expect(all).not.toContain("mudur");
    expect(stored()).toHaveLength(1);
  });

  it("clears the record when the request is refused", async () => {
    await open([{ status: 202, body: pending("g1") }, { status: 403, body: { error: { code: "GRANT_DENIED", message: "x" } } }]);
    await sendDiscount();

    submitDiscount().click();
    await waitFor(() => expect(text()).toContain("İndirim talebiniz reddedildi."));

    expect(stored()).toEqual([]);
  });

  it("works without storage: a blocked sessionStorage only loses the refresh restore", async () => {
    const original = Object.getOwnPropertyDescriptor(window, "sessionStorage");
    Object.defineProperty(window, "sessionStorage", { configurable: true, get() { throw new Error("blocked"); } });
    try {
      await open([{ status: 202, body: pending("g1") }]);
      await sendDiscount();
      expect(submitDiscount().textContent).toBe("Onay durumunu kontrol et");
    } finally {
      Object.defineProperty(window, "sessionStorage", original);
    }
  });
});
