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

const unpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };
const applied = { status: "Applied", billId: BILL_ID, adjustmentId: "a1", summary: {}, grantId: null };
const pending = { status: "Pending", billId: BILL_ID, adjustmentId: null, summary: null, grantId: "g1" };

// Each element answers one POST .../discount in order.
async function open(discountAnswers, delayMs = 0) {
  const answers = [...discountAnswers];
  const routeSet = [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: unpaid },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    {
      test: (url, init) => url.endsWith("/discount") && init?.method === "POST",
      delayMs,
      get status() { return answers[0].status; },
      get body() { return answers.shift().body; },
    },
  ];
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter(routeSet);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect(document.getElementById("submit-discount")).not.toBeNull());
  return fetchMock;
}

const discountCalls = (fetchMock) => fetchMock.mock.calls
  .filter(([url, init]) => url.endsWith("/discount") && init?.method === "POST")
  .map(([, init]) => JSON.parse(init.body));
const type = (id, value) => {
  const input = document.getElementById(id);
  input.value = value;
  input.dispatchEvent(new Event("input", { bubbles: true }));
};
const submit = () => document.getElementById("submit-discount");
const text = () => document.getElementById("app").textContent;
const settle = () => new Promise((done) => setTimeout(done, 20));

describe("split-payment.js discount waiting for a manager", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("keeps the request and its key while a manager decides and applies the discount when the check finds it approved", async () => {
    const fetchMock = await open([{ status: 202, body: pending }, { status: 202, body: pending }, { status: 200, body: applied }]);

    type("discount-value", "20");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    expect(submit().textContent).toBe("Onay durumunu kontrol et");
    expect(document.getElementById("discount-value").value).toBe("20");

    submit().click();
    await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));
    await settle();
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("İndirim uygulandı ve kaydedildi."));

    const calls = discountCalls(fetchMock);
    expect(calls).toHaveLength(3);
    expect(new Set(calls.map((c) => c.IdempotencyKey)).size).toBe(1);
    expect(new Set(calls.map((c) => c.Value)).size).toBe(1);
  });

  it("checks a waiting full-bill discount without asking for the confirmation again", async () => {
    const fetchMock = await open([{ status: 202, body: pending }, { status: 200, body: applied }]);

    type("discount-value", "100");
    submit().click();
    expect(discountCalls(fetchMock)).toHaveLength(0);
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    submit().click();
    await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));
  });

  it("asks for the full-bill confirmation when a waiting discount is edited to take the whole bill", async () => {
    const fetchMock = await open([{ status: 202, body: pending }]);

    type("discount-value", "20");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    type("discount-value", "100");
    submit().click();

    expect(discountCalls(fetchMock)).toHaveLength(1);
    expect(text()).toContain("kapatır");
  });

  it("ignores a waiting answer for a form that was edited while the request was in flight", async () => {
    const fetchMock = await open([{ status: 202, body: pending }], 80);

    type("discount-value", "20");
    submit().click();
    type("discount-value", "100");
    await new Promise((done) => setTimeout(done, 150));

    expect(text()).not.toContain("yönetici onayına gönderildi");
    expect(text()).not.toContain("İndirim uygulandı");
    expect(submit().textContent).toBe("İndirim uygula");
    submit().click();
    expect(discountCalls(fetchMock)).toHaveLength(1);
    expect(text()).toContain("kapatır");
  });

  it("keeps what was typed while the request was in flight when the discount is applied", async () => {
    await open([{ status: 200, body: applied }], 80);

    type("discount-value", "20");
    submit().click();
    type("discount-value", "7");
    await vi.waitFor(() => expect(text()).toContain("İndirim uygulandı ve kaydedildi."));

    expect(document.getElementById("discount-value").value).toBe("7");
  });

  it.each([
    ["discount-reason", "change", "VIPGuest"],
    ["discount-calc-type", "change", "FixedAmount"],
    ["discount-note", "input", "başka not"],
  ])("starts a new request when %s of the waiting discount is edited", async (id, eventName, value) => {
    const fetchMock = await open([{ status: 202, body: pending }, { status: 202, body: pending }]);

    type("discount-value", "20");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    const field = document.getElementById(id);
    field.value = field.tagName === "SELECT" ? [...field.options].find((o) => o.value !== field.value).value : value;
    field.dispatchEvent(new Event(eventName, { bubbles: true }));
    expect(submit().textContent).toBe("İndirim uygula");
    submit().click();
    await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));

    const [first, second] = discountCalls(fetchMock);
    expect(second.IdempotencyKey).not.toBe(first.IdempotencyKey);
  });

  it("keeps the waiting state when checking the approval fails on the server", async () => {
    const fetchMock = await open([{ status: 202, body: pending }, { status: 503, body: { error: { code: "X", message: "x" } } }]);

    type("discount-value", "20");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    submit().click();
    await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));
    await settle();

    expect(submit().textContent).toBe("Onay durumunu kontrol et");
    const [first, second] = discountCalls(fetchMock);
    expect(second.IdempotencyKey).toBe(first.IdempotencyKey);
  });

  it("shows no error for a refusal that belongs to a form edited in the meantime", async () => {
    await open([{ status: 403, body: { error: { code: "GRANT_DENIED", message: "x" } } }], 80);

    type("discount-value", "20");
    submit().click();
    type("discount-value", "7");
    await new Promise((done) => setTimeout(done, 150));

    expect(text()).not.toContain("reddedildi");
  });

  it("starts a new request when the waiting discount is edited", async () => {
    const fetchMock = await open([{ status: 202, body: pending }, { status: 202, body: pending }]);

    type("discount-value", "20");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    type("discount-value", "15");
    expect(submit().textContent).toBe("İndirim uygula");
    expect(document.getElementById("discount-notice")).toBeNull();
    submit().click();
    await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));

    const [first, second] = discountCalls(fetchMock);
    expect(second.IdempotencyKey).not.toBe(first.IdempotencyKey);
  });

  it("does not reuse the key of a refused discount", async () => {
    const refused = { error: { code: "GRANT_DENIED", message: "x" } };
    const fetchMock = await open([{ status: 403, body: refused }, { status: 200, body: applied }]);

    type("discount-value", "20");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("İndirim talebiniz reddedildi."));
    submit().click();
    await vi.waitFor(() => expect(discountCalls(fetchMock)).toHaveLength(2));

    const [first, second] = discountCalls(fetchMock);
    expect(second.IdempotencyKey).not.toBe(first.IdempotencyKey);
  });

  it("tells a discount refused because of the bill state with the server's sentence for that action", async () => {
    await open([{ status: 409, body: { error: { code: "UNSUPPORTED_BILL_STATE", message: "Bu hesap durumunda indirim uygulanamaz." } } }]);

    type("discount-value", "20");
    submit().click();

    await vi.waitFor(() => expect(text()).toContain("Bu hesap durumunda indirim uygulanamaz."));
    expect(text()).not.toContain("bölme tasarımı");
  });
});
