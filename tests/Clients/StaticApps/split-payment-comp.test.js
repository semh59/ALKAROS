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

// Each element answers one POST .../comp in order.
async function open(compAnswers) {
  const answers = [...compAnswers];
  const routeSet = [
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: unpaid },
    { test: (url) => url.includes("/cash-sessions/active"), body: { cashSessionId: "s1" } },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    {
      test: (url, init) => url.endsWith("/comp") && init?.method === "POST",
      get status() { return answers[0].status; },
      get body() { return answers.shift().body; },
    },
  ];
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const fetchMock = installFetchRouter(routeSet);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect(document.getElementById("submit-comp")).not.toBeNull());
  return fetchMock;
}

const compCalls = (fetchMock) => fetchMock.mock.calls
  .filter(([url, init]) => url.endsWith("/comp") && init?.method === "POST")
  .map(([, init]) => JSON.parse(init.body));
const type = (id, value) => {
  const input = document.getElementById(id);
  input.value = value;
  input.dispatchEvent(new Event("input", { bubbles: true }));
};
const submit = () => document.getElementById("submit-comp");
const text = () => document.getElementById("fold-comp").textContent;
const settle = () => new Promise((done) => setTimeout(done, 20));

describe("split-payment.js comp (ikram)", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("offers Turkish comp reasons and fills the whole remaining amount on request", async () => {
    await open([]);

    const reasons = [...document.querySelectorAll("#comp-reason option")].map((o) => o.textContent);
    expect(reasons).toEqual(["Müşteri memnuniyeti", "Yönetici ikramı", "Özel misafir", "Hizmet özrü"]);
    document.getElementById("comp-all").click();
    expect(Number(document.getElementById("comp-amount").value)).toBe(100);
  });

  it("sends amount, reason and a key, then confirms an applied comp", async () => {
    const fetchMock = await open([{ status: 200, body: applied }]);

    type("comp-amount", "30");
    document.getElementById("comp-reason").value = "VIPGuest";
    document.getElementById("comp-reason").dispatchEvent(new Event("change", { bubbles: true }));
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("İkram uygulandı ve kaydedildi."));

    const [call] = compCalls(fetchMock);
    expect(call).toMatchObject({ Amount: 30, ReasonCode: "VIPGuest", Notes: null });
    expect(call.IdempotencyKey).toBeTruthy();
  });

  it("keeps the same key while a manager decides and applies the comp when the check finds it approved", async () => {
    const fetchMock = await open([{ status: 202, body: pending }, { status: 202, body: pending }, { status: 200, body: applied }]);

    type("comp-amount", "80");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    expect(submit().textContent).toBe("Onay durumunu kontrol et");

    submit().click();
    await vi.waitFor(() => expect(compCalls(fetchMock)).toHaveLength(2));
    await settle();
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("İkram uygulandı ve kaydedildi."));

    const keys = compCalls(fetchMock).map((c) => c.IdempotencyKey);
    expect(new Set(keys).size).toBe(1);
    expect(keys).toHaveLength(3);
  });

  it("starts a new request when the pending comp is edited", async () => {
    const fetchMock = await open([{ status: 202, body: pending }, { status: 202, body: pending }]);

    type("comp-amount", "80");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("yönetici onayına gönderildi"));
    type("comp-amount", "70");
    document.getElementById("comp-all").click();
    expect(submit().textContent).toBe("İkram et");
    type("comp-amount", "70");
    submit().click();
    await vi.waitFor(() => expect(compCalls(fetchMock)).toHaveLength(2));

    const [first, second] = compCalls(fetchMock);
    expect(second.IdempotencyKey).not.toBe(first.IdempotencyKey);
  });

  it("answers a refused comp in Turkish and does not reuse the decided key", async () => {
    const refused = { error: { code: "GRANT_DENIED", message: "x" } };
    const fetchMock = await open([{ status: 403, body: refused }, { status: 200, body: applied }]);

    type("comp-amount", "20");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("İkram talebiniz reddedildi."));
    expect(document.getElementById("fold-comp").textContent).not.toContain("GRANT_DENIED");
    submit().click();
    await vi.waitFor(() => expect(compCalls(fetchMock)).toHaveLength(2));

    const [first, second] = compCalls(fetchMock);
    expect(second.IdempotencyKey).not.toBe(first.IdempotencyKey);
  });

  it("keeps the key after a network failure so the retry cannot create a second comp", async () => {
    const fetchMock = await open([{ status: 200, body: applied }]);
    const real = fetchMock.getMockImplementation();
    let fail = true;
    fetchMock.mockImplementation(async (url, init) => {
      if (fail && url.endsWith("/comp")) { fail = false; throw new Error("offline"); }
      return real(url, init);
    });

    type("comp-amount", "20");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("Bağlantı kurulamadı. Tekrar deneyin."));
    expect(document.getElementById("comp-amount").disabled).toBe(false);
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("İkram uygulandı ve kaydedildi."));

    const keys = compCalls(fetchMock).map((c) => c.IdempotencyKey);
    expect(keys).toHaveLength(2);
    expect(keys[1]).toBe(keys[0]);
  });

  it("refuses a missing amount or one above the remaining amount without calling the server", async () => {
    const fetchMock = await open([]);

    submit().click();
    await vi.waitFor(() => expect(text()).toContain("İkram tutarı sıfırdan büyük olmalı."));
    type("comp-amount", "100.01");
    submit().click();
    await vi.waitFor(() => expect(text()).toContain("hesabın kalan tutarını aşamaz"));

    expect(compCalls(fetchMock)).toHaveLength(0);
  });

  it("asks once before a comp that closes the whole bill", async () => {
    const fetchMock = await open([{ status: 200, body: applied }]);

    document.getElementById("comp-all").click();
    submit().click();
    await vi.waitFor(() => expect(submit().textContent).toBe("Evet, ikram et"));
    expect(compCalls(fetchMock)).toHaveLength(0);
    submit().click();
    await vi.waitFor(() => expect(compCalls(fetchMock)).toHaveLength(1));
  });
});
