import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fileURLToPath } from "node:url";
import { readFileSync } from "node:fs";
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

/** A scripted server; `world.down` lists the URL endings whose request fails at the network level. */
function install(world) {
  vi.stubGlobal("fetch", vi.fn(async (url, init) => {
    const method = init?.method ?? "GET";
    if (world.down.some((ending) => url.endsWith(ending))) throw new TypeError("Failed to fetch");
    if (url.endsWith("/auth/session/current")) {
      return world.signedOut ? reply(401, {}) : reply(200, { userId: "u1", displayName: "Ayşe", terminalId: TERMINAL, capabilities: [] });
    }
    if (url.endsWith("/cash-sessions/active")) return world.noSession ? reply(404, {}) : reply(200, session);
    if (url.endsWith("/expected-cash")) {
      return world.expectedUnreadable ? reply(500, {}) : reply(200, { expectedCash: 1000, varianceTolerance: 50, openBillCount: 0, unsettledPaymentCount: 0 });
    }
    if (url.endsWith("/cash-movements") && method === "GET") return reply(200, emptyList);
    if (url.endsWith("/start-count")) return reply(200, { ...session, status: "Counting" });
    if (url.endsWith("/counts")) return reply(200, {});
    throw new Error(`unrouted ${method} ${url}`);
  }));
}

async function type(selector, value) {
  const element = $(selector);
  element.value = value;
  element.dispatchEvent(new Event("input", { bubbles: true }));
}

const LOST = "Bağlantı kesildi.";

describe("cash-session.js connection-lost messages say what happened and what to do", () => {
  beforeEach(() => {
    vi.resetModules();
    window.history.replaceState({}, "", "/");
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    document.body.innerHTML = "";
  });

  it("login: nothing was sent, try again", async () => {
    install({ down: ["/auth/login"], signedOut: true });
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#login-form")).not.toBeNull());
    await type("#username", "ayse");
    await type("#password", "x");
    $("#login-form").dispatchEvent(new Event("submit", { bubbles: true, cancelable: true }));

    await vi.waitFor(() => expect(text()).toContain(LOST));
    expect(text()).toContain("Giriş yapılamadı");
    expect(text()).toContain("tekrar deneyin");
  });

  it("opening the drawer: it may have been opened, refresh and check before opening again", async () => {
    install({ down: ["/cash-sessions"], noSession: true });
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#open-btn")).not.toBeNull());
    $("#open-btn").click();

    await vi.waitFor(() => expect(text()).toContain("Kasa açılmış olabilir"));
    expect(text()).toContain(LOST);
    expect(text()).toContain("sayfayı yenileyip kasa durumunu kontrol edin");
  });

  it("movement check screen: nothing was saved yet, try again", async () => {
    const world = { down: [] };
    install(world);
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#cash-in")).not.toBeNull());
    $("#cash-in").click();
    await vi.waitFor(() => expect($("#movement-amount")).not.toBeNull());
    await type("#movement-amount", "25");
    await type("#movement-notes", "market");
    world.down.push("/expected-cash");
    $("#movement-submit").click();

    await vi.waitFor(() => expect(text()).toContain(LOST));
    expect(text()).toContain("Hareket henüz kaydedilmedi");
  });

  it("starting the count: it may have started, refresh and check", async () => {
    install({ down: ["/start-count"] });
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#start-count")).not.toBeNull());
    $("#start-count").click();
    $("#start-count-confirm").click();

    await vi.waitFor(() => expect(text()).toContain(LOST));
    expect(text()).toContain("Sayım başlamış olabilir");
    expect(text()).toContain("sayfayı yenileyip kasa durumunu kontrol edin");
  });

  it("sending the count: it may not be saved, send it again (the latest count counts)", async () => {
    const world = { down: [] };
    install(world);
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#start-count")).not.toBeNull());
    $("#start-count").click();
    $("#start-count-confirm").click();
    await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
    await type("#denom-100", "10");
    world.down.push("/counts");
    $("#submit-count").click();

    await vi.waitFor(() => expect(text()).toContain(LOST));
    expect(text()).toContain("Sayımınız kaydedilmemiş olabilir");
    expect(text()).toContain("sayımı tekrar gönderin");
  });

  it("re-reading the expected amount: it only reads, so just check the connection and try again", async () => {
    const world = { down: [], expectedUnreadable: true };
    install(world);
    await loadApp(htmlPath, scriptPath);
    await vi.waitFor(() => expect($("#start-count")).not.toBeNull());
    $("#start-count").click();
    $("#start-count-confirm").click();
    await vi.waitFor(() => expect($("#denom-100")).not.toBeNull());
    await type("#denom-100", "10");
    $("#submit-count").click();
    await vi.waitFor(() => expect($("#reread-expected")).not.toBeNull());
    world.down.push("/expected-cash");
    $("#reread-expected").click();

    await vi.waitFor(() => expect(text()).toContain(LOST));
    expect(text()).toContain("Beklenen tutar okunamadı");
    expect(text()).not.toContain("kaydedilmemiş olabilir");
  });

  it("says every connection loss with the one wording, never 'Sunucuya ulaşılamadı' or 'Bağlantı koptu'", () => {
    const source = readFileSync(scriptPath, "utf8");

    expect(source).not.toMatch(/Sunucuya ulaşılamadı|Bağlantı koptu/);
    expect([...source.matchAll(/'(Bağlantı kesildi\.[^']*)'/g)].length).toBeGreaterThanOrEqual(11);
  });
});
