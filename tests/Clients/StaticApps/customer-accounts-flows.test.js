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
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
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
