import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";
import { loadApp } from "./support/loadApp.js";

const here = dirname(fileURLToPath(import.meta.url));
const pageDir = resolve(here, "..", "..", "..", "src/Clients/Cashier/wwwroot/payments/cash-session");
const htmlPath = resolve(pageDir, "index.html");
const scriptPath = resolve(pageDir, "cash-session.js");

const TERMINAL = "11111111-1111-1111-1111-111111111111";
const SESSION = "22222222-2222-2222-2222-222222222222";
// A figure no other number on these screens can produce: the page formats it as "1.234,56".
const EXPECTED = 1234.56;
const EXPECTED_TEXT = /1\.234,56/;
const reply = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body, headers: { get: () => null } });
const $ = (selector) => document.querySelector(selector);
const text = () => document.body.textContent;

/** A page reload in the middle of the count: the session is `Counting`, `lastCountedAmount` is what the server holds. */
function install({ lastCountedAmount = null, ownerId = "u1", expectedFails = false, capabilities = [], closeThrows = false } = {}) {
  const calls = [];
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    calls.push({ url, method, body: init?.body ? JSON.parse(init.body) : null });
    if (url.endsWith("/auth/session/current")) return reply(200, { userId: "u1", displayName: "Ayşe", terminalId: TERMINAL, capabilities });
    if (url.endsWith("/cash-sessions/active/owner")) return reply(200, { displayName: "Mehmet Usta" });
    if (url.endsWith("/cash-sessions/active")) {
      return reply(200, { cashSessionId: SESSION, cashierUserId: ownerId, status: "Counting", openingBalance: 1000, openedAt: "2026-11-03T08:00:00Z" });
    }
    if (url.endsWith("/expected-cash")) {
      if (expectedFails) return reply(500, {});
      return reply(200, { expectedCash: EXPECTED, varianceTolerance: 50, openBillCount: 0, unsettledPaymentCount: 0, lastCountedAmount });
    }
    if (url.endsWith("/close") && closeThrows) throw new TypeError("network");
    if (url.endsWith("/close")) return reply(200, { session: { cashSessionId: SESSION, closedAt: "2026-11-03T20:00:00Z", openedAt: "2026-11-03T08:00:00Z" }, difference: 0 });
    throw new Error(`unrouted ${method} ${url}`);
  }));
  return calls;
}

describe("cash-session.js reloaded while the session is counting", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("resumes at the difference check with the recorded count and says so, without asking for the count again", async () => {
    const calls = install({ lastCountedAmount: 1100 });
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());

    expect(text()).toMatch(EXPECTED_TEXT);
    expect(text()).toContain("1.100,00");
    expect($("#cs-notice").textContent).toContain("Kayıtlı sayımınızla devam ediyorsunuz");
    expect($("#submit-count")).toBeNull();
    expect(calls.filter((call) => call.url.endsWith("/counts"))).toEqual([]);
  });

  it("closes with the recorded count and sends no new count", async () => {
    const calls = install({ lastCountedAmount: 1234.56 });
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());

    $("#submit-close").click();
    await vi.waitFor(() => expect($("#cs-exit")).not.toBeNull());

    expect(calls.filter((call) => call.url.endsWith("/close")).map((call) => call.body.ActualCash)).toEqual([1234.56]);
    expect(calls.filter((call) => call.url.endsWith("/counts"))).toEqual([]);
  });

  it("opens the blind count form when no count was recorded, and the expected cash is nowhere on the page", async () => {
    install({ lastCountedAmount: null });
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#submit-count")).not.toBeNull());

    expect(text()).not.toMatch(EXPECTED_TEXT);
    expect(document.body.innerHTML).not.toMatch(/1234|1\.234/);
    expect($("#submit-close")).toBeNull();
  });

  it("says the saved count could not be read instead of opening a blank form that would record a correction", async () => {
    install({ expectedFails: true });
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#cs-reload")).not.toBeNull());

    expect($(".cs-alert-body").textContent).toContain("Kayıtlı sayım okunamadı");
    expect($("#submit-count")).toBeNull();
    expect($("#submit-close")).toBeNull();
  });

  it("lets a manager who may act for the owner resume at the difference check", async () => {
    install({ lastCountedAmount: 1100, ownerId: "u2", capabilities: ["cash.session.override"] });
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());

    expect(text()).toMatch(EXPECTED_TEXT);
  });

  it("drops the resume sentence when the cashier chooses to recount, and the recount form shows no expected cash", async () => {
    install({ lastCountedAmount: 1100 });
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#recount")).not.toBeNull());

    $("#recount").click();

    expect($("#submit-count")).not.toBeNull();
    expect($("#cs-notice")).toBeNull();
    expect(document.body.innerHTML).not.toMatch(/1234|1\.234/);
  });

  it("keeps the connection-lost sentence of a close that may not have landed instead of the resume sentence", async () => {
    install({ lastCountedAmount: EXPECTED, closeThrows: true });
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());

    $("#submit-close").click();
    await vi.waitFor(() => expect($("#cs-notice").textContent).toContain("Kasa kapanmış olabilir"));

    expect($("#cs-notice").textContent).not.toContain("Kayıtlı sayımınızla");
    expect($("#submit-close")).not.toBeNull();
  });

  it("answers the no-sale link with the counting sentence and does not open the form later", async () => {
    window.history.replaceState({}, "", "/?open=no-sale");
    install({ lastCountedAmount: 1100 });
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());

    expect(text()).toContain("Kasa sayımı sürerken bu ekrandan satışsız çekmece açılamaz.");
    expect($("#no-sale-form")).toBeNull();
    expect(window.location.search).toBe("");
  });

  it("leaves another cashier's counting session on the blocked count view and does not read the expected cash", async () => {
    const calls = install({ lastCountedAmount: 1100, ownerId: "u2" });
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#submit-count")).not.toBeNull());

    expect($("#submit-close")).toBeNull();
    expect(text()).not.toMatch(EXPECTED_TEXT);
    expect(calls.some((call) => call.url.endsWith("/expected-cash"))).toBe(false);
  });
});
