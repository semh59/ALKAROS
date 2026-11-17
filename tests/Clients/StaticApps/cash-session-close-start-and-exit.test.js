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
const session = { cashSessionId: SESSION, status: "Open", openingBalance: 1000, openedAt: "2026-11-03T08:00:00Z" };
const emptyList = { movements: [], movementsTruncated: false, requests: [], approvalThreshold: 500, selfApprovalEnabled: false };

const reply = (status, body) => ({ ok: status >= 200 && status < 300, status, json: async () => body, headers: { get: () => null } });
const $ = (selector) => document.querySelector(selector);
const text = () => document.body.textContent;

function install({ logout = reply(204, null) } = {}) {
  const calls = [];
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    calls.push({ url, method });
    if (url.endsWith("/auth/session/current")) return reply(200, { userId: "u1", displayName: "Ayşe", terminalId: TERMINAL, capabilities: [] });
    if (url.endsWith("/cash-sessions/active")) return reply(200, session);
    if (url.endsWith("/expected-cash")) return reply(200, { expectedCash: 1000, varianceTolerance: 50, openBillCount: 0, unsettledPaymentCount: 0 });
    if (url.endsWith("/cash-movements") && method === "GET") return reply(200, emptyList);
    if (url.endsWith("/start-count")) return reply(200, { ...session, status: "Counting" });
    if (url.endsWith("/counts")) return reply(200, {});
    if (url.endsWith("/close")) return reply(200, { session: { ...session, closedAt: "2026-11-03T20:00:00Z" }, difference: 0 });
    if (url.includes("/auth/logout")) return typeof logout === "function" ? logout() : logout;
    throw new Error(`unrouted ${method} ${url}`);
  }));
  return calls;
}

const startCalls = (calls) => calls.filter((c) => c.url.endsWith("/start-count"));
const logoutCalls = (calls) => calls.filter((c) => c.url.includes("/auth/logout"));

async function type(selector, value) {
  const element = $(selector);
  element.value = value;
  element.dispatchEvent(new Event("input", { bubbles: true }));
}

async function openDrawerScreen(options) {
  const calls = install(options);
  await loadApp(htmlPath, scriptPath);
  await vi.waitFor(() => expect($("#start-count")).not.toBeNull());
  return calls;
}

async function closeTheShift(calls) {
  $("#start-count").click();
  $("#start-count-confirm").click();
  await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
  await type("#denom-100", "10");
  $("#submit-count").click();
  await vi.waitFor(() => expect($("#submit-close")).not.toBeNull());
  $("#submit-close").click();
  await vi.waitFor(() => expect($("#reopen")).not.toBeNull());
  return calls;
}

describe("cash-session.js starting the count", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("asks once, says what the count locks, and sends nothing until the cashier says yes", async () => {
    const calls = await openDrawerScreen();

    $("#start-count").click();

    expect($("#start-count-confirm").textContent).toBe("Evet, sayıma başla");
    expect($("#start-count-cancel").textContent).toBe("Vazgeç");
    expect(text()).toContain("Sayıma başlayınca kasada yeni satış ve nakit hareketi yapılamaz. Devam edilsin mi?");
    expect(startCalls(calls)).toHaveLength(0);
  });

  it("goes back to the drawer screen when the cashier says no, and asks again next time", async () => {
    const calls = await openDrawerScreen();

    $("#start-count").click();
    $("#start-count-cancel").click();

    expect($("#start-count")).not.toBeNull();
    expect($("#start-count-confirm")).toBeNull();
    expect(startCalls(calls)).toHaveLength(0);
    $("#start-count").click();
    expect($("#start-count-confirm")).not.toBeNull();
  });

  it("starts the count on the one yes", async () => {
    const calls = await openDrawerScreen();

    $("#start-count").click();
    $("#start-count-confirm").click();

    await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
    expect(startCalls(calls)).toHaveLength(1);
  });

  it("shows the drawer button again, not the question, when the count could not be started", async () => {
    await openDrawerScreen();
    const inner = fetch.getMockImplementation();
    fetch.mockImplementation(async (url, init) => (String(url).endsWith("/start-count") ? reply(500, { error: { code: "INTERNAL_ERROR", message: "RAW" } }) : inner(url, init)));

    $("#start-count").click();
    $("#start-count-confirm").click();

    await vi.waitFor(() => expect(text()).toContain("Sunucu tarafında bir sorun oluştu"));
    expect($("#start-count").disabled).toBe(false);
    expect($("#start-count-confirm")).toBeNull();
    expect(fetch.mock.calls.filter(([url]) => String(url).endsWith("/start-count"))).toHaveLength(1);
  });
});

describe("cash-session.js start-count question does not come back by itself", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("is dropped when the screen is redrawn for something else", async () => {
    await openDrawerScreen();

    $("#start-count").click();
    expect($("#start-count-confirm")).not.toBeNull();
    $("#refresh-movements").click();
    await vi.waitFor(() => expect($("#refresh-movements")).not.toBeNull());
    await vi.waitFor(() => expect($("#refresh-movements").disabled).toBe(false));

    expect($("#start-count-confirm")).toBeNull();
    expect($("#start-count")).not.toBeNull();
  });
});

describe("cash-session.js after the shift is closed", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("makes leaving the primary action and keeps a new shift as the secondary one", async () => {
    await closeTheShift(await openDrawerScreen());

    expect($("#cs-exit").className).toContain("cs-btn-primary");
    expect($("#cs-exit").textContent).toBe("Çıkış yap");
    expect($("#reopen").className).toContain("cs-btn-secondary");
    expect($("#reopen").textContent).toBe("Yeni Vardiya Aç");
  });

  it("signs the cashier out of this terminal and returns to the sign-in page", async () => {
    const assign = vi.fn();
    vi.stubGlobal("location", { ...window.location, search: "", assign });
    const calls = await closeTheShift(await openDrawerScreen());

    $("#cs-exit").click();

    await vi.waitFor(() => expect(assign).toHaveBeenCalledWith("/"));
    expect(logoutCalls(calls)).toHaveLength(1);
    expect(logoutCalls(calls)[0].method).toBe("POST");
    expect(logoutCalls(calls)[0].url).toContain(`terminalId=${TERMINAL}`);
  });

  it("treats a session that already ended as signed out", async () => {
    const assign = vi.fn();
    vi.stubGlobal("location", { ...window.location, search: "", assign });
    await closeTheShift(await openDrawerScreen({ logout: reply(401, null) }));

    $("#cs-exit").click();

    await vi.waitFor(() => expect(assign).toHaveBeenCalledWith("/"));
    expect(text()).not.toContain("Çıkış yapılamadı");
  });

  it("locks the new-shift button while signing out", async () => {
    let release;
    const gate = new Promise((resolve) => { release = resolve; });
    await closeTheShift(await openDrawerScreen({ logout: () => gate }));

    $("#cs-exit").click();

    await vi.waitFor(() => expect($("#reopen").disabled).toBe(true));
    release(reply(500, null));
    await vi.waitFor(() => expect($("#reopen").disabled).toBe(false));
  });

  it("says in Turkish when signing out failed, stays on the page and leaves the new-shift button usable", async () => {
    const calls = await closeTheShift(await openDrawerScreen({ logout: reply(500, { error: { code: "INTERNAL_ERROR", message: "RAW" } }) }));

    $("#cs-exit").click();

    await vi.waitFor(() => expect(text()).toContain("Çıkış yapılamadı. Tekrar deneyin."));
    expect(text()).not.toContain("RAW");
    expect(logoutCalls(calls)).toHaveLength(1);
    expect($("#reopen").disabled).toBe(false);
  });
});
