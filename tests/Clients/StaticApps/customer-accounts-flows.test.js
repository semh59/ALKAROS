import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/customer-accounts");
const htmlPath = resolve(pageDir, "index.html");
const scriptPath = resolve(pageDir, "customer-accounts.js");

const TERMINAL = "11111111-1111-1111-1111-111111111111";
const CUSTOMER = "33333333-3333-3333-3333-333333333333";
const SESSION = "44444444-4444-4444-4444-444444444444";

const customer = { customerId: CUSTOMER, name: "Ali Veli", phoneMasked: null, balance: 250, creditLimit: 1000, availableCredit: 750, paymentTermDays: null, taxIdKind: null };
const statement = { customer, entries: [], receipts: [] };
const reply = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body, headers: { get: () => null } });
const $ = (id) => document.getElementById(id);
const text = () => document.body.textContent;

/** `routes` overrides single answers: key "METHOD suffix" -> () => response (or a promise of one). */
async function open({ capabilities = ["payments.take", "cash.drawer"], routes = {} } = {}) {
  const calls = [];
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    const body = init?.body ? JSON.parse(init.body) : null;
    calls.push({ method, url, body });
    for (const [key, answer] of Object.entries(routes)) {
      const [m, suffix] = key.split(" ");
      if (m === method && url.endsWith(suffix)) return answer(body);
    }
    if (url.endsWith("/auth/session/current")) return reply(200, { terminalId: TERMINAL, userId: "u1", capabilities });
    if (url.endsWith("/cash-sessions/active")) return reply(200, { cashSessionId: SESSION });
    if (url.endsWith("/customers") && method === "GET") return reply(200, [customer]);
    if (url.endsWith("/statement")) return reply(200, statement);
    if (url.endsWith("/account-receipts")) return reply(200, { amount: 50, receiptNumber: "M-1", balanceAfter: 200 });
    if (url.endsWith("/credit-terms")) return reply(200, {});
    throw new Error(`unrouted ${method} ${url}`);
  }));
  window.history.replaceState({}, "", "/");
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect(document.querySelector(".sp-customer")).not.toBeNull());
  document.querySelector(".sp-customer").click();
  await vi.waitFor(() => expect($("ca-receive")).not.toBeNull());
  return calls;
}

function type(id, value) {
  $(id).value = value;
  $(id).dispatchEvent(new Event("input", { bubbles: true }));
}

describe("customer-accounts.js", () => {
  const shownListeners = [];
  beforeEach(() => {
    vi.resetModules();
    const add = document.addEventListener.bind(document);
    vi.spyOn(document, "addEventListener").mockImplementation((type, listener, options) => {
      if (type === "visibilitychange") shownListeners.push(listener);
      add(type, listener, options);
    });
  });
  afterEach(() => {
    shownListeners.splice(0).forEach((listener) => document.removeEventListener("visibilitychange", listener));
    vi.restoreAllMocks();
    delete document.visibilityState;
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("reads the list and the open customer again when the page is shown again, keeping what is being typed", async () => {
    let balance = 250;
    const calls = await open({
      routes: {
        "GET /customers": () => reply(200, [{ ...customer, balance }]),
        "GET /statement": () => reply(200, { ...statement, customer: { ...customer, balance } }),
      },
    });
    const cardDebt = () => document.querySelector("section .sp-summary-row.is-total .value").textContent;
    expect(cardDebt()).toContain("250,00");
    const listDebt = () => document.querySelector(".sp-customer .sp-customer-meta").textContent;
    expect(listDebt()).toContain("250,00");
    type("ca-receipt-amount", "50");
    balance = 280;
    const before = calls.length;

    Object.defineProperty(document, "visibilityState", { value: "visible", configurable: true });
    document.dispatchEvent(new Event("visibilitychange"));

    await vi.waitFor(() => expect(cardDebt()).toContain("280,00"));
    expect(listDebt()).toContain("280,00");
    expect(calls.slice(before).map((call) => call.url.split("/").slice(-1)[0])).toEqual(expect.arrayContaining(["customers", "statement"]));
    expect($("ca-receipt-amount").value).toBe("50");
  });

  it("does not read again while the page is hidden", async () => {
    const calls = await open();
    const before = calls.length;

    Object.defineProperty(document, "visibilityState", { value: "hidden", configurable: true });
    document.dispatchEvent(new Event("visibilitychange"));

    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(calls.length).toBe(before);
  });

  it("drops a refresh answer that a newer customer choice overtook", async () => {
    let release;
    const gate = new Promise((resolve) => { release = resolve; });
    let slow = false;
    const calls = await open({
      routes: {
        "GET /statement": () => (slow ? gate.then(() => reply(200, { ...statement, customer: { ...customer, balance: 999 } })) : reply(200, statement)),
      },
    });
    const field = $("ca-receipt-amount");
    field.focus();
    field.value = "12345";
    slow = true;

    Object.defineProperty(document, "visibilityState", { value: "visible", configurable: true });
    document.dispatchEvent(new Event("visibilitychange"));
    await vi.waitFor(() => expect(calls.filter((call) => call.url.endsWith("/statement")).length).toBeGreaterThan(1));
    slow = false;
    document.querySelector(".sp-customer").click();
    await vi.waitFor(() => expect(calls.filter((call) => call.url.endsWith("/statement")).length).toBeGreaterThan(2));
    release();
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(document.querySelector("section .sp-summary-row.is-total .value").textContent).toContain("250,00");
  });

  it("logs a refresh that could not reach the server and leaves the screen as it was", async () => {
    const error = vi.spyOn(console, "error").mockImplementation(() => undefined);
    let down = false;
    await open({
      routes: {
        "GET /customers": () => (down ? Promise.reject(new TypeError("offline")) : reply(200, [customer])),
        "GET /statement": () => (down ? Promise.reject(new TypeError("offline")) : reply(200, statement)),
      },
    });
    down = true;

    Object.defineProperty(document, "visibilityState", { value: "visible", configurable: true });
    document.dispatchEvent(new Event("visibilitychange"));

    await vi.waitFor(() => expect(error).toHaveBeenCalledWith("Müşteri bilgisi yenilenemedi:", expect.any(TypeError)));
    expect(document.querySelector("section .sp-summary-row.is-total .value").textContent).toContain("250,00");
  });

  it("puts the focus back in the field after a refresh drew the page again", async () => {
    const calls = await open();
    const field = $("ca-receipt-amount");
    field.focus();
    field.value = "12345";
    field.dispatchEvent(new Event("input", { bubbles: true }));
    const before = calls.length;

    Object.defineProperty(document, "visibilityState", { value: "visible", configurable: true });
    document.dispatchEvent(new Event("visibilitychange"));
    await vi.waitFor(() => expect(calls.length).toBeGreaterThan(before + 1));
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(document.activeElement).toBe($("ca-receipt-amount"));
    expect($("ca-receipt-amount").value).toBe("12345");
  });

  it("keeps the screen when the refresh fails and does not touch a running action", async () => {
    let fail = false;
    const calls = await open({
      routes: {
        "GET /customers": () => (fail ? reply(500, {}) : reply(200, [customer])),
        "GET /statement": () => (fail ? reply(500, {}) : reply(200, statement)),
      },
    });
    fail = true;

    Object.defineProperty(document, "visibilityState", { value: "visible", configurable: true });
    document.dispatchEvent(new Event("visibilitychange"));
    await vi.waitFor(() => expect(calls.filter((call) => call.url.endsWith("/statement")).length).toBeGreaterThan(1));
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(document.querySelector("section .sp-summary-row.is-total .value").textContent).toContain("250,00");
    expect(document.querySelector('[role="alert"]')).toBeNull();
  });

  it("shows the credit limit form only to a session that may change it", async () => {
    await open({ capabilities: ["payments.take", "cash.drawer"] });
    expect($("ca-limit")).toBeNull();
    expect($("ca-save-limit")).toBeNull();
    expect($("ca-limit-hint").textContent).toContain("yalnız yönetici");

    document.body.innerHTML = "";
    vi.resetModules();
    await open({ capabilities: ["payments.take", "cash.drawer", "settings.manage"] });
    expect($("ca-limit")).not.toBeNull();
    expect($("ca-save-limit")).not.toBeNull();
  });

  it.each([
    [["payments.take", "cash.drawer"], "Kredi limiti 0: bu müşteriye hesaba yazılamaz. Limiti yalnız yönetici belirleyebilir."],
    [["payments.take", "cash.drawer", "settings.manage"], "Kredi limiti 0: bu müşteriye hesaba yazılamaz. Aşağıdan bir limit belirleyin."],
  ])("says plainly that a customer with limit 0 cannot be charged to the account (%j)", async (capabilities, note) => {
    await open({ capabilities, routes: { "GET /statement": () => reply(200, { ...statement, customer: { ...customer, creditLimit: 0, availableCredit: 0 } }) } });
    expect($("ca-zero-limit").textContent).toBe(note);
  });

  it("shows no zero-limit note when the customer has a limit", async () => {
    await open();
    expect($("ca-zero-limit")).toBeNull();
  });

  it("does not save an emptied limit as 0: it asks for a value and sends nothing", async () => {
    const calls = await open({ capabilities: ["settings.manage", "payments.take"] });

    type("ca-limit", "");
    $("ca-save-limit").click();

    expect(text()).toContain("Kredi limitini yazın");
    expect(calls.some((call) => call.method === "PUT" && call.url.endsWith("/credit-terms"))).toBe(false);
  });

  it("still saves a deliberate 0 limit", async () => {
    const calls = await open({ capabilities: ["settings.manage", "payments.take"] });

    type("ca-limit", "0");
    $("ca-save-limit").click();

    await vi.waitFor(() => expect(calls.find((call) => call.method === "PUT")?.body.CreditLimit).toBe(0));
  });

  it("sends a session lost while saving the limit back to the sign-in prompt", async () => {
    await open({ capabilities: ["settings.manage", "payments.take"], routes: { "PUT /credit-terms": () => reply(401, {}) } });

    type("ca-limit", "500");
    $("ca-save-limit").click();

    await vi.waitFor(() => expect($("ca-save-limit")).toBeNull());
    expect(text()).not.toContain("Kredi limiti kaydedildi");
  });

  it("draws the busy state while a cash receipt is being sent", async () => {
    let release;
    await open({
      routes: { "POST /account-receipts": () => new Promise((resolveReply) => { release = () => resolveReply(reply(200, { amount: 50, receiptNumber: "M-1", balanceAfter: 200 })); }) },
    });
    type("ca-receipt-amount", "50");

    $("ca-receive").click();

    expect($("ca-receive").disabled).toBe(true);
    expect($("app").getAttribute("aria-busy")).toBe("true");
    release();
    await vi.waitFor(() => expect(text()).toContain("Tahsil edildi"));
  });

  it.each([
    ["CUSTOMER_NOT_FOUND", 404, "Müşteri bulunamadı."],
    ["CONCURRENT_UPDATE", 409, "Müşteri kaydı başka bir kasada değişti. Listeyi yenileyip tekrar deneyin."],
    ["CLOSED_CASH_SESSION", 409, "Kasa oturumu açık değil."],
  ])("shows the fixed sentence of %s, not the server's text", async (code, status, sentence) => {
    await open({ routes: { "POST /account-receipts": () => reply(status, { error: { code, message: "raw server text" } }) } });
    type("ca-receipt-amount", "50");

    $("ca-receive").click();

    await vi.waitFor(() => expect(text()).toContain(sentence));
    expect(text()).not.toContain("raw server text");
  });

  it("keeps the server's Turkish sentence for field validation, which names the field to fix", async () => {
    await open({ routes: { "POST /account-receipts": () => reply(400, { error: { code: "VALIDATION_FAILED", message: "Vergi numarası 10 haneli olmalıdır." } }) } });
    type("ca-receipt-amount", "50");

    $("ca-receive").click();

    await vi.waitFor(() => expect(text()).toContain("Vergi numarası 10 haneli olmalıdır."));
  });

  it("falls back to the status sentence for a code it does not know", async () => {
    await open({ routes: { "POST /account-receipts": () => reply(500, { error: { code: "SOMETHING_NEW", message: "raw server text" } }) } });
    type("ca-receipt-amount", "50");

    $("ca-receive").click();

    await vi.waitFor(() => expect(text()).toContain("Sunucu tarafında bir sorun oluştu"));
    expect(text()).not.toContain("raw server text");
  });

  it("writes statement times in Istanbul time whatever the device zone is", async () => {
    process.env.TZ = "UTC";
    await open({
      routes: {
        "GET /statement": () => reply(200, {
          customer,
          entries: [{ occurredAt: "2026-09-24T21:30:00Z", description: "Satış", signedAmount: 100 }],
          receipts: [{ receiptNumber: "M-9", amount: 40, issuedAt: "2026-09-24T21:30:00Z" }],
        }),
      },
    });

    expect(text()).toContain("25.09.2026 00:30");
    expect(text()).not.toContain("24.09.2026 21:30");
  });
});

// The till's own clock on a parked ticket lives in cashier-app.js; it shares this file because it is the same
// "Istanbul time on the device" rule as the statement above.
describe("cashier-app.js parked ticket time", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.useRealTimers();
    vi.unstubAllGlobals();
    localStorage.clear();
    document.body.innerHTML = "";
  });

  it("stamps a parked ticket with the Istanbul time", async () => {
    process.env.TZ = "UTC";
    const root = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot");
    vi.stubGlobal("fetch", vi.fn(async (url) => {
      if (url.endsWith("/auth/session/current")) return reply(200, { terminalId: TERMINAL, displayName: "Kasiyer" });
      return reply(200, []);
    }));
    localStorage.setItem("alkaros_cashier_active_ticket", JSON.stringify([{ productId: "p", name: "Kola", price: 45, quantity: 1 }]));
    await loadApp(resolve(root, "index.html"), resolve(root, "cashier-app.js"));
    await vi.waitFor(() => expect(document.getElementById("btnParkTicket")).not.toBeNull());
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-09-24T11:32:00Z"));

    document.getElementById("btnParkTicket").click();

    expect(JSON.parse(localStorage.getItem("alkaros_cashier_parked"))[0].parkedAt).toBe("14:32");
  });
});
