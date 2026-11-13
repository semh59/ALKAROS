import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { readFileSync } from "node:fs";
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
const SESSION_ID = "66666666-6666-6666-6666-666666666666";
const unpaid = { billId: BILL_ID, payableAmount: 100, allocatedTotal: 0, remainingAmount: 100, allocations: [], unsettledPayment: null };

const app = () => document.getElementById("app");
const text = () => app().textContent;
const chip = (method) => document.querySelector(`.sp-method-chip[data-method="${method}"]`);
const checkedChips = () => [...document.querySelectorAll('.sp-method-chip[aria-checked="true"]')];

/** `cashRoute` is the answer of GET .../cash-sessions/active; `reject` makes that call fail at the network level. */
async function open({ cashRoute = { status: 404 }, reject = false, extra = [] } = {}) {
  window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
  const routes = [
    ...extra,
    { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
    { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: unpaid },
    { test: (url) => url.includes("/cash-sessions/active"), ...cashRoute },
    { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
    { test: (url) => url.endsWith("/refund-requests"), body: [] },
    { test: (url) => url.endsWith("/show-on-display"), status: 204 },
  ];
  const router = installFetchRouter(routes);
  if (reject) {
    vi.stubGlobal("fetch", vi.fn(async (url, init) => {
      if (String(url).includes("/cash-sessions/active")) throw new TypeError("Failed to fetch");
      return router(url, init);
    }));
  }
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect(chip("Eft")).not.toBeNull());
  return router;
}

describe("split-payment.js cash session state", () => {
  beforeEach(() => vi.resetModules());
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("selects cash when the cash session is open", async () => {
    await open({ cashRoute: { body: { cashSessionId: SESSION_ID } } });

    expect(chip("Cash").disabled).toBe(false);
    expect(checkedChips().map((c) => c.dataset.method)).toEqual(["Cash"]);
    expect(document.getElementById("cash-state-notice")).toBeNull();
  });

  it("another cashier's open session: cash is off and the cashier is told to open their own drawer first", async () => {
    await open({ cashRoute: { body: { cashSessionId: SESSION_ID, cashierUserId: "someone-else" } } });

    expect(chip("Cash").disabled).toBe(true);
    expect(checkedChips()).toEqual([]);
    const notice = document.getElementById("cash-state-notice");
    expect(notice.textContent).toContain("Önce kasayı açın");
    expect(notice.textContent).toContain("size ait değil");
    expect(notice.textContent).toContain("önce kapatmalı");
    expect(chip("Cash").textContent).toContain("kasa size ait değil");
  });

  it("a server refusal for someone else's session shows the open-your-drawer sentence", async () => {
    await open({
      cashRoute: { body: { cashSessionId: SESSION_ID, cashierUserId: "u1" } },
      extra: [{ test: (url, init) => url.endsWith("/cash-tender") && init?.method === "POST", status: 409, body: { error: { code: "CASH_SESSION_NOT_OWNED", message: "x" } } }],
    });

    document.getElementById("submit-tender").click();

    await vi.waitFor(() => expect(text()).toContain("Önce kasayı açın: terminalde tek kasa oturumu olur"));
  });

  it("the cashier's own open session keeps cash selectable", async () => {
    await open({ cashRoute: { body: { cashSessionId: SESSION_ID, cashierUserId: "u1" } } });

    expect(chip("Cash").disabled).toBe(false);
    expect(document.getElementById("cash-state-notice")).toBeNull();
  });

  it("closed session: cash is off, no method is chosen for the cashier, the way to open the drawer is offered", async () => {
    await open({ cashRoute: { status: 404 } });

    expect(chip("Cash").disabled).toBe(true);
    expect(checkedChips()).toEqual([]);
    expect(document.getElementById("eft-confirm")).toBeNull();
    expect(document.getElementById("submit-tender").disabled).toBe(true);
    expect(document.getElementById("method-hint").textContent).toContain("yöntem seçin");
    const notice = document.getElementById("cash-state-notice");
    expect(notice.textContent).toContain("Kasa oturumu kapalı");
    const link = notice.querySelector("a");
    expect(link.textContent).toBe("Kasayı aç");
    expect(link.getAttribute("href")).toBe("/cashier/payments/cash-session/index.html");
    expect(chip("Cash").textContent).toContain("kasa kapalı");
  });

  it.each([
    [500, "Sunucu tarafında bir sorun oluştu"],
    [503, "Sunucu tarafında bir sorun oluştu"],
    [429, "Çok fazla istek gönderildi"],
  ])("a %i while reading the cash session is not 'closed': it gets its own warning and a retry", async (status, sentence) => {
    await open({ cashRoute: { status, body: { error: { message: "raw server text" } } } });

    const notice = document.getElementById("cash-state-notice");
    expect(notice.textContent).toContain("Kasa durumu okunamadı");
    expect(notice.textContent).toContain(sentence);
    expect(notice.textContent).not.toContain("raw server text");
    expect(notice.textContent).not.toContain("Kasa oturumu kapalı");
    expect(document.getElementById("retry-cash-state")).not.toBeNull();
    expect(chip("Cash").disabled).toBe(true);
    expect(chip("Cash").textContent).not.toContain("kasa kapalı");
    expect(checkedChips()).toEqual([]);
  });

  it("a 403 means this user has no drawer permission: said so plainly, no pointless retry, other methods stay open", async () => {
    await open({ cashRoute: { status: 403, body: {} } });

    const notice = document.getElementById("cash-state-notice");
    expect(notice.textContent).toContain("kasa izniniz yok");
    expect(document.getElementById("retry-cash-state")).toBeNull();
    expect(chip("Cash").disabled).toBe(true);
    expect(chip("Cash").textContent).toContain("kasa izni yok");
    chip("Eft").click();
    expect(checkedChips().map((c) => c.dataset.method)).toEqual(["Eft"]);
  });

  it("a network failure reading the cash session keeps the bill on screen and warns", async () => {
    await open({ reject: true });

    expect(text()).toContain("Tahsilat");
    expect(document.getElementById("cash-state-notice").textContent).toContain("Sunucuya ulaşılamadı");
    expect(checkedChips()).toEqual([]);
  });

  it("choosing EFT explicitly works while the drawer is closed", async () => {
    await open({ cashRoute: { status: 404 } });

    chip("Eft").click();

    expect(checkedChips().map((c) => c.dataset.method)).toEqual(["Eft"]);
    expect(document.getElementById("eft-confirm")).not.toBeNull();
  });

  it("the retry button reads the cash session again and enables cash once it is open", async () => {
    let answer = { status: 500 };
    window.history.replaceState({}, "", `/?billId=${BILL_ID}`);
    installFetchRouter([
      { test: (url) => url.endsWith("/api/v1/auth/session/current"), body: { terminalId: TERMINAL_ID, userId: "u1" } },
      { test: (url, init) => url.endsWith("/tenders/") && (init?.method ?? "GET") === "GET", body: unpaid },
      { test: (url) => url.includes("/cash-sessions/active"), get status() { return answer.status; }, get body() { return answer.body ?? {}; } },
      { test: (url) => url.endsWith("/adjustments"), body: { adjustments: [], summary: null } },
      { test: (url) => url.endsWith("/refund-requests"), body: [] },
      { test: (url) => url.endsWith("/show-on-display"), status: 204 },
    ]);
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect(document.getElementById("retry-cash-state")).not.toBeNull());

    answer = { status: 200, body: { cashSessionId: SESSION_ID } };
    document.getElementById("retry-cash-state").click();

    await vi.waitFor(() => expect(chip("Cash").disabled).toBe(false));
    expect(document.getElementById("cash-state-notice")).toBeNull();
    expect(checkedChips().map((c) => c.dataset.method)).toEqual(["Cash"]);
  });

  it("the disabled cash chip is drawn with readable colours, not with a faded opacity", () => {
    const css = readFileSync(resolve(pageDir, "split-payment.css"), "utf8");
    const rule = css.match(/\.sp-method-chip:disabled \{([^}]*)\}/)[1];

    expect(rule).not.toMatch(/opacity/);
    expect(rule).toMatch(/color:\s*var\(--color-text-muted\)/);
  });
});
